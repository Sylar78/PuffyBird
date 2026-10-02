using PuffyBird.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.UI
{
    /// <summary>Ce que déclenche un bouton de l'interface.</summary>
    public enum UiAction
    {
        None,
        Pause,
        OpenSettings,
        CloseMenu,
        GoHome,
        Continue,
        ToggleMusic,
        ToggleSound,
        ToggleHaptics,
        CycleQuality,
        OpenPrivacy,
        ConsentAccept,
        ConsentRefuse,
        OpenPrivacyPolicy,
        OpenLeaderboard,
        Share,
        OpenSkins,
        PlayDaily,
        SkinPrevious,
        SkinNext,
        SkinUse,
        SkinBuy,
        RemoveAds,
        RestorePurchases,
        ToggleReduceFlash,
        ToggleHighContrast,
    }

    /// <summary>
    /// Briques communes de l'interface en volume : textes et panneaux posés en pixels logiques
    /// juste devant le plan de jeu, et boutons. Chaque image, les vues redéclarent les boutons
    /// qu'elles affichent (<see cref="BeginFrame"/>, <see cref="AddButton"/>) ; un tap est testé
    /// contre ceux de l'image affichée (<see cref="HitTest"/>). Aucune allocation après le chargement.
    /// </summary>
    public sealed class UiLayer
    {
        public const float HudZ = -1.2f;
        public const float PanelZ = -1.05f;
        const int MaxButtons = 32;

        public sealed class Element
        {
            public GameObject GameObject;
            public Transform Transform;
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            /// <summary>Largeur du texte en voxels (0 si l'élément n'est pas un texte construit par <see cref="Text"/>).</summary>
            public float TextWidth;
        }

        /// <summary>Bouton dessiné : cadre sombre, fond coloré, libellé.</summary>
        public sealed class Button
        {
            public Element Border;
            public Element Fill;
            public Element Label;
        }

        struct Hit
        {
            public UiAction Action;
            public Rect Rect;
        }

        readonly WorldSpace _space;
        readonly Transform _root;
        readonly Hit[] _hits = new Hit[MaxButtons];
        int _hitCount;

        public UiLayer(Transform parent, MaterialLibrary materials, WorldSpace space)
        {
            _space = space;
            _root = new GameObject("Interface").transform;
            _root.SetParent(parent, false);

            TextMaterial = materials.Lit("Texte", Color.white, 0.5f, 0f, 0.2f);
            TextMaterial.SetFloat(MaterialLibrary.VertexEmission, 0.35f);
            PanelMaterial = materials.Lit("Panneau", Color.white, 0.3f, 0f, 0.1f);
            PanelMaterial.SetFloat(MaterialLibrary.VertexEmission, 0.25f);
            Box = new MeshBuilder().AddBox(Vector3.zero, Vector3.one, Color.white).Build("Boîte");
        }

        public WorldSpace Space => _space;
        public Transform Root => _root;
        public Material TextMaterial { get; }
        public Material PanelMaterial { get; }
        public Mesh Box { get; }

        // ───────── construction (au chargement) ─────────

        public Element Create(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return new Element { GameObject = go, Transform = go.transform, Renderer = r, Filter = filter };
        }

        public Element Text(string text, TextAlign align, Color front, Color outline)
        {
            var e = Create(text, VoxelFont.Build(text, align, front, outline), TextMaterial);
            e.TextWidth = VoxelFont.Width(text);
            return e;
        }

        public Element Solid(Color color)
        {
            var e = Create("Panneau", Box, PanelMaterial);
            SetColor(e, color);
            return e;
        }

        public static void SetColor(Element e, Color color)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(MaterialLibrary.BaseColor, color);
            e.Renderer.SetPropertyBlock(mpb);
        }

        public Button CreateButton(string label, Color fill, Color text)
        {
            return new Button
            {
                Border = Solid(Palette.Outline),
                Fill = Solid(fill),
                Label = label == null ? null : Text(label, TextAlign.Center, text, Palette.Outline),
            };
        }

        // ───────── placement (chaque image) ─────────

        /// <summary>
        /// Taille de voxel d'un texte réduite si besoin pour qu'il tienne dans <paramref name="maxWidth"/>
        /// (par défaut l'écran moins une marge) : les traductions n'ont pas toutes la longueur du français.
        /// </summary>
        float Fit(Element e, float voxelPx, float maxWidth)
        {
            if (e.TextWidth <= 0f) return voxelPx;
            float limit = maxWidth > 0f ? maxWidth : _space.Config.Width - 16f;
            return e.TextWidth * voxelPx > limit ? limit / e.TextWidth : voxelPx;
        }

        public void Place(Element e, float px, float py, float voxelPx, float z = HudZ, float maxWidth = 0f)
        {
            voxelPx = Fit(e, voxelPx, maxWidth);
            var p = _space.OnScreen(px, py, z, out float t);
            float s = voxelPx / WorldSpace.PixelsPerUnit * t;
            e.Transform.localPosition = p;
            e.Transform.localScale = new Vector3(s, s, s);
            Show(e);
        }

        public void PlaceBox(Element e, float left, float top, float width, float height, float z)
        {
            var p = _space.OnScreen(left + width * 0.5f, top + height * 0.5f, z, out float t);
            e.Transform.localPosition = p;
            e.Transform.localScale = new Vector3(width / WorldSpace.PixelsPerUnit * t, height / WorldSpace.PixelsPerUnit * t, 0.04f);
            Show(e);
        }

        /// <summary>
        /// Bouton centré en (cx, cy), cliquable sur sa surface agrandie de <paramref name="hitMargin"/>
        /// de chaque côté. <paramref name="label"/> remplace le libellé du bouton s'il est fourni.
        /// </summary>
        public void PlaceButton(Button b, UiAction action, float cx, float cy, float width, float height,
            float labelVoxelPx = 1.6f, Element label = null, float hitMargin = 4f)
        {
            float left = cx - width * 0.5f;
            float top = cy - height * 0.5f;
            PlaceBox(b.Border, left - 2f, top - 2f, width + 4f, height + 4f, HudZ + 0.12f);
            PlaceBox(b.Fill, left, top, width, height, HudZ + 0.08f);
            var text = label ?? b.Label;
            if (text != null)
            {
                float fitted = Fit(text, labelVoxelPx, width - 10f);
                Place(text, cx, cy - VoxelFont.GlyphHeight * fitted * 0.5f, fitted);
            }
            AddButton(action, cx, cy, width + hitMargin * 2f, height + hitMargin * 2f);
        }

        public static void Hide(Button b)
        {
            Hide(b.Border);
            Hide(b.Fill);
            if (b.Label != null) Hide(b.Label);
        }

        public static void Show(Element e)
        {
            if (!e.GameObject.activeSelf) e.GameObject.SetActive(true);
        }

        public static void Hide(Element e)
        {
            if (e != null && e.GameObject.activeSelf) e.GameObject.SetActive(false);
        }

        // ───────── boutons ─────────

        /// <summary>À appeler une fois par image, avant que les vues déclarent leurs boutons.</summary>
        public void BeginFrame() => _hitCount = 0;

        public void AddButton(UiAction action, float cx, float cy, float width, float height)
        {
            if (_hitCount >= MaxButtons) return;
            _hits[_hitCount++] = new Hit { Action = action, Rect = new Rect(cx - width * 0.5f, cy - height * 0.5f, width, height) };
        }

        /// <summary>Bouton sous le point logique donné ; le dernier déclaré (au premier plan) l'emporte.</summary>
        public UiAction HitTest(Vector2 logical)
        {
            for (int i = _hitCount - 1; i >= 0; i--)
            {
                if (_hits[i].Rect.Contains(logical)) return _hits[i].Action;
            }
            return UiAction.None;
        }

        /// <summary>Vrai si au moins un bouton est affiché (un menu ouvert bloque alors les taps du jeu).</summary>
        public bool HasButtons => _hitCount > 0;
    }
}
