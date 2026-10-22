using ElementalPhenix.Core;

namespace ElementalPhenix.UI
{
    /// <summary>
    /// Textes du Voyage des éléments, dans les cinq langues : nom de chaque monde, règle de l'étape
    /// et histoire (deux lignes par monde : le phénix rallume les neuf éléments un à un).
    /// Lus au chargement seulement.
    /// </summary>
    public static class JourneyTexts
    {
        /// <summary>Nom d'une étape du Voyage (son décor).</summary>
        public static string StageName(Theme theme)
        {
            switch (theme)
            {
                case Theme.Night: return Lang.T("NUIT ÉTOILÉE", "STARRY NIGHT", "NOCHE ESTRELLADA", "STERNENNACHT", "NOITE ESTRELADA");
                case Theme.City: return Lang.T("VILLE NÉON", "NEON CITY", "CIUDAD NEÓN", "NEONSTADT", "CIDADE NEON");
                case Theme.Japan: return Lang.T("JAPON", "JAPAN", "JAPÓN", "JAPAN", "JAPÃO");
                case Theme.Storm: return Lang.T("ORAGE", "STORM", "TORMENTA", "GEWITTER", "TEMPESTADE");
                case Theme.Snow: return Lang.T("GRAND NORD", "FAR NORTH", "GRAN NORTE", "HOHER NORDEN", "EXTREMO NORTE");
                case Theme.Jungle: return Lang.T("JUNGLE", "JUNGLE", "SELVA", "DSCHUNGEL", "SELVA");
                case Theme.Sky: return Lang.T("CIEL", "SKY", "CIELO", "HIMMEL", "CÉU");
                case Theme.Ocean: return Lang.T("ABYSSES", "ABYSS", "ABISMO", "TIEFSEE", "ABISMO");
                default: return Lang.T("PRAIRIE", "MEADOW", "PRADERA", "WIESE", "PRADARIA");
            }
        }

        /// <summary>Règle d'une étape du Voyage, en quelques mots (voir <see cref="GameConfig.Journey"/>).</summary>
        public static string RuleName(StageRule rule)
        {
            switch (rule)
            {
                case StageRule.MoreStars: return Lang.T("PLUS D'ÉTOILES", "MORE STARS", "MÁS ESTRELLAS", "MEHR STERNE", "MAIS ESTRELAS");
                case StageRule.DenseTraffic: return Lang.T("TRAFIC DENSE", "HEAVY TRAFFIC", "TRÁFICO DENSO", "DICHTER VERKEHR", "TRÂNSITO INTENSO");
                case StageRule.NarrowGates: return Lang.T("PORTES ÉTROITES", "NARROW GATES", "PUERTAS ESTRECHAS", "ENGE TORE", "PORTAS ESTREITAS");
                case StageRule.Gusts: return Lang.T("RAFALES DE VENT", "WIND GUSTS", "RÁFAGAS DE VIENTO", "WINDBÖEN", "RAJADAS DE VENTO");
                case StageRule.FrozenWings: return Lang.T("AILES GELÉES", "FROZEN WINGS", "ALAS HELADAS", "GEFRORENE FLÜGEL", "ASAS CONGELADAS");
                case StageRule.WildRush: return Lang.T("COURSE FOLLE", "WILD RUSH", "CARRERA LOCA", "WILDE JAGD", "CORRIDA LOUCA");
                case StageRule.LightAir: return Lang.T("AIR LÉGER", "LIGHT AIR", "AIRE LIGERO", "LEICHTE LUFT", "AR LEVE");
                case StageRule.SlowSwim: return Lang.T("NAGE AU RALENTI, COURANTS", "SLOW SWIM, CURRENTS", "NADO LENTO, CORRIENTES", "LANGSAM SCHWIMMEN, STRÖMUNGEN", "NADO LENTO, CORRENTES");
                default: return Lang.T("PREMIER ENVOL", "FIRST FLIGHT", "PRIMER VUELO", "ERSTER FLUG", "PRIMEIRO VOO");
            }
        }

        /// <summary>Histoire d'un monde : ligne 0 ou 1.</summary>
        public static string Story(Theme theme, int line)
        {
            bool first = line == 0;
            switch (theme)
            {
                case Theme.Night:
                    return first
                        ? Lang.T("LA NUIT A PERDU SES ÉTOILES.", "THE NIGHT HAS LOST ITS STARS.", "LA NOCHE PERDIÓ SUS ESTRELLAS.", "DIE NACHT HAT IHRE STERNE VERLOREN.", "A NOITE PERDEU SUAS ESTRELAS.")
                        : Lang.T("RAMÈNE LEUR LUMIÈRE AU CIEL.", "BRING THEIR LIGHT BACK TO THE SKY.", "DEVUELVE SU LUZ AL CIELO.", "BRING IHR LICHT ZURÜCK.", "DEVOLVA A LUZ AO CÉU.");
                case Theme.City:
                    return first
                        ? Lang.T("LES NÉONS DE LA VILLE S'ÉTEIGNENT.", "THE CITY'S NEON LIGHTS ARE FADING.", "LOS NEONES DE LA CIUDAD SE APAGAN.", "DIE NEONLICHTER DER STADT ERLÖSCHEN.", "OS NEONS DA CIDADE SE APAGAM.")
                        : Lang.T("FAUFILE-TOI DANS LE TRAFIC.", "WEAVE THROUGH THE TRAFFIC.", "ESQUIVA EL TRÁFICO.", "SCHLÄNGEL DICH DURCH DEN VERKEHR.", "DESVIE DO TRÂNSITO.");
                case Theme.Japan:
                    return first
                        ? Lang.T("LES TEMPLES ONT FERMÉ LEURS PORTES.", "THE TEMPLES HAVE SHUT THEIR GATES.", "LOS TEMPLOS CERRARON SUS PUERTAS.", "DIE TEMPEL HABEN IHRE TORE GESCHLOSSEN.", "OS TEMPLOS FECHARAM SUAS PORTAS.")
                        : Lang.T("PASSE LES PORTES SANS LES TOUCHER.", "SLIP THROUGH WITHOUT A TOUCH.", "PASA SIN ROZARLAS.", "GLEITE HINDURCH OHNE BERÜHRUNG.", "PASSE SEM TOCAR NELAS.");
                case Theme.Storm:
                    return first
                        ? Lang.T("L'ORAGE GARDE LA FLAMME DU VENT.", "THE STORM HOLDS THE FLAME OF WIND.", "LA TORMENTA GUARDA LA LLAMA DEL VIENTO.", "DAS GEWITTER HÜTET DIE FLAMME DES WINDES.", "A TEMPESTADE GUARDA A CHAMA DO VENTO.")
                        : Lang.T("TIENS BON DANS LES RAFALES.", "HOLD STEADY IN THE GUSTS.", "RESISTE LAS RÁFAGAS.", "HALTE DICH IN DEN BÖEN.", "AGUENTE AS RAJADAS.");
                case Theme.Snow:
                    return first
                        ? Lang.T("LE GRAND NORD A GELÉ TES AILES.", "THE FAR NORTH HAS FROZEN YOUR WINGS.", "EL GRAN NORTE HELÓ TUS ALAS.", "DER HOHE NORDEN HAT DEINE FLÜGEL VEREIST.", "O EXTREMO NORTE CONGELOU SUAS ASAS.")
                        : Lang.T("BATS PLUS FORT POUR LES RÉCHAUFFER.", "FLAP HARDER TO WARM THEM UP.", "ALETEA MÁS FUERTE PARA CALENTARLAS.", "SCHLAG STÄRKER, UM SIE ZU WÄRMEN.", "BATA MAIS FORTE PARA AQUECÊ-LAS.");
                case Theme.Jungle:
                    return first
                        ? Lang.T("LA JUNGLE S'EMBALLE.", "THE JUNGLE IS RACING.", "LA SELVA SE DESBOCA.", "DER DSCHUNGEL RAST.", "A SELVA DISPARA.")
                        : Lang.T("SUIS SON RYTHME SANS FAIBLIR.", "KEEP UP WITHOUT FALTERING.", "SIGUE SU RITMO SIN FALLAR.", "HALTE MIT, OHNE ZU WANKEN.", "SIGA O RITMO SEM FALHAR.");
                case Theme.Sky:
                    return first
                        ? Lang.T("AU-DESSUS DES NUAGES, L'AIR EST LÉGER.", "ABOVE THE CLOUDS, THE AIR IS LIGHT.", "SOBRE LAS NUBES, EL AIRE ES LIGERO.", "ÜBER DEN WOLKEN IST DIE LUFT LEICHT.", "ACIMA DAS NUVENS, O AR É LEVE.")
                        : Lang.T("DOSE CHAQUE BATTEMENT.", "MEASURE EVERY WINGBEAT.", "MIDE CADA ALETEO.", "DOSIERE JEDEN FLÜGELSCHLAG.", "DOSE CADA BATIDA DE ASAS.");
                case Theme.Ocean:
                    return first
                        ? Lang.T("LA DERNIÈRE FLAMME DORT SOUS LA MER.", "THE LAST FLAME SLEEPS BENEATH THE SEA.", "LA ÚLTIMA LLAMA DUERME BAJO EL MAR.", "DIE LETZTE FLAMME SCHLÄFT IM MEER.", "A ÚLTIMA CHAMA DORME SOB O MAR.")
                        : Lang.T("LAISSE LES COURANTS TE PORTER.", "LET THE CURRENTS CARRY YOU.", "DEJA QUE LAS CORRIENTES TE LLEVEN.", "LASS DICH VON DEN STRÖMUNGEN TRAGEN.", "DEIXE AS CORRENTES TE LEVAREM.");
                default:
                    return first
                        ? Lang.T("LA FLAMME DU MONDE VACILLE.", "THE WORLD'S FLAME IS FLICKERING.", "LA LLAMA DEL MUNDO VACILA.", "DIE FLAMME DER WELT FLACKERT.", "A CHAMA DO MUNDO VACILA.")
                        : Lang.T("RALLUME LA PRAIRIE, PETIT PHÉNIX.", "REKINDLE THE MEADOW, YOUNG PHOENIX.", "REAVIVA LA PRADERA, PEQUEÑO FÉNIX.", "ENTFACHE DIE WIESE, KLEINER PHÖNIX.", "REACENDA A PRADARIA, PEQUENA FÊNIX.");
            }
        }

        /// <summary>Fin de l'histoire, sous « VOYAGE TERMINÉ ! ».</summary>
        public static string Epilogue => Lang.T("LES NEUF ÉLÉMENTS BRILLENT À NOUVEAU !", "ALL NINE ELEMENTS SHINE AGAIN!", "¡LOS NUEVE ELEMENTOS BRILLAN DE NUEVO!", "ALLE NEUN ELEMENTE LEUCHTEN WIEDER!", "OS NOVE ELEMENTOS BRILHAM DE NOVO!");
    }
}
