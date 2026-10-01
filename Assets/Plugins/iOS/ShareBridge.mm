// Pont natif du partage pour PuffyBird.Social.ShareService : feuille de partage iOS.
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController();

extern "C"
{
    void _Share_Text(const char* text, const char* url)
    {
        UIViewController* root = UnityGetGLViewController();
        if (root == nil || text == NULL) return;

        NSMutableArray* items = [NSMutableArray arrayWithObject:[NSString stringWithUTF8String:text]];
        if (url != NULL && url[0] != 0) [items addObject:[NSURL URLWithString:[NSString stringWithUTF8String:url]]];

        UIActivityViewController* sheet = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        // iPad : la feuille s'ouvre en bulle, ancrée en bas au centre de l'écran.
        UIPopoverPresentationController* popover = sheet.popoverPresentationController;
        if (popover != nil)
        {
            popover.sourceView = root.view;
            CGRect bounds = root.view.bounds;
            popover.sourceRect = CGRectMake(CGRectGetMidX(bounds), CGRectGetMaxY(bounds) * 0.66, 1, 1);
            popover.permittedArrowDirections = UIPopoverArrowDirectionDown;
        }
        [root presentViewController:sheet animated:YES completion:nil];
    }
}
