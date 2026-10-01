// Pont natif Game Center pour PuffyBird.Social.Leaderboard : connexion, envoi du score, classement.
#import <GameKit/GameKit.h>

extern UIViewController* UnityGetGLViewController();

// Fenêtre de connexion fournie par Game Center : présentée seulement au tap sur le trophée.
static UIViewController* gSignInController;

@interface PBGameCenterDelegate : NSObject <GKGameCenterControllerDelegate>
@end

@implementation PBGameCenterDelegate
- (void)gameCenterViewControllerDidFinish:(GKGameCenterViewController*)controller
{
    [controller dismissViewControllerAnimated:YES completion:nil];
}
@end

static PBGameCenterDelegate* gDelegate;

extern "C"
{
    void _GC_Authenticate()
    {
        [GKLocalPlayer localPlayer].authenticateHandler = ^(UIViewController* controller, NSError* error)
        {
            gSignInController = controller;
            if (error != nil) NSLog(@"PuffyBird : Game Center indisponible (%@)", error.localizedDescription);
        };
    }

    bool _GC_IsAuthenticated()
    {
        return [GKLocalPlayer localPlayer].isAuthenticated;
    }

    void _GC_Submit(const char* leaderboard, long long score)
    {
        if (![GKLocalPlayer localPlayer].isAuthenticated || leaderboard == NULL) return;
        NSString* identifier = [NSString stringWithUTF8String:leaderboard];
        void (^done)(NSError*) = ^(NSError* error)
        {
            if (error != nil) NSLog(@"PuffyBird : score non envoyé (%@)", error.localizedDescription);
        };
        if (@available(iOS 14.0, *))
        {
            [GKLeaderboard submitScore:(NSInteger)score context:0 player:[GKLocalPlayer localPlayer]
                        leaderboardIDs:@[identifier] completionHandler:done];
        }
        else
        {
            GKScore* entry = [[GKScore alloc] initWithLeaderboardIdentifier:identifier];
            entry.value = score;
            [GKScore reportScores:@[entry] withCompletionHandler:done];
        }
    }

    void _GC_Show(const char* leaderboard)
    {
        UIViewController* root = UnityGetGLViewController();
        if (root == nil || leaderboard == NULL) return;

        if (![GKLocalPlayer localPlayer].isAuthenticated)
        {
            if (gSignInController != nil)
            {
                [root presentViewController:gSignInController animated:YES completion:nil];
                gSignInController = nil;
            }
            else
            {
                // Connexion refusée plus tôt : Game Center ne la repropose pas, on ouvre les Réglages.
                [[UIApplication sharedApplication] openURL:[NSURL URLWithString:UIApplicationOpenSettingsURLString]
                                                   options:@{} completionHandler:nil];
            }
            return;
        }

        NSString* identifier = [NSString stringWithUTF8String:leaderboard];
        GKGameCenterViewController* controller;
        if (@available(iOS 14.0, *))
        {
            controller = [[GKGameCenterViewController alloc] initWithLeaderboardID:identifier
                                                                       playerScope:GKLeaderboardPlayerScopeGlobal
                                                                         timeScope:GKLeaderboardTimeScopeAllTime];
        }
        else
        {
            controller = [[GKGameCenterViewController alloc] init];
            controller.viewState = GKGameCenterViewControllerStateLeaderboards;
            controller.leaderboardIdentifier = identifier;
        }
        if (gDelegate == nil) gDelegate = [[PBGameCenterDelegate alloc] init];
        controller.gameCenterDelegate = gDelegate;
        [root presentViewController:controller animated:YES completion:nil];
    }
}
