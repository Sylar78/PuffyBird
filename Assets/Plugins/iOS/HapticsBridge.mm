// Pont natif des vibrations pour PuffyBird.Feedback.Haptics : générateurs d'impact UIKit.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator* gGenerators[3];

extern "C"
{
    void _Haptics_Prepare()
    {
        UIImpactFeedbackStyle styles[3] = { UIImpactFeedbackStyleLight, UIImpactFeedbackStyleMedium, UIImpactFeedbackStyleHeavy };
        for (int i = 0; i < 3; i++)
        {
            if (gGenerators[i] == nil) gGenerators[i] = [[UIImpactFeedbackGenerator alloc] initWithStyle:styles[i]];
            [gGenerators[i] prepare];
        }
    }

    // 0 = léger, 1 = moyen, 2 = fort.
    void _Haptics_Impact(int style)
    {
        if (style < 0 || style > 2) return;
        UIImpactFeedbackGenerator* generator = gGenerators[style];
        if (generator == nil) return;
        [generator impactOccurred];
        [generator prepare];
    }
}
