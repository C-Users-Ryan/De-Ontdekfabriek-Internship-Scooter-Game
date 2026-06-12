// HapticPlugin.mm
// Native iOS plugin for Unity — accesses the Taptic Engine.
// Place in Assets/OvertakeGame/Plugins/iOS/
// Unity compiles this automatically when building for iOS.

#import <UIKit/UIKit.h>

extern "C" {
    // style: 0=Light, 1=Medium, 2=Heavy
    void _TriggerImpactHaptic(int style) {
        UIImpactFeedbackStyle s;
        switch(style) {
            case 0:  s = UIImpactFeedbackStyleLight;  break;
            case 2:  s = UIImpactFeedbackStyleHeavy;  break;
            default: s = UIImpactFeedbackStyleMedium; break;
        }
        UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        [g prepare];
        [g impactOccurred];
    }

    // type: 0=Success, 1=Warning, 2=Error
    void _TriggerNotificationHaptic(int type) {
        UINotificationFeedbackType t;
        switch(type) {
            case 0:  t = UINotificationFeedbackTypeSuccess; break;
            case 1:  t = UINotificationFeedbackTypeWarning; break;
            default: t = UINotificationFeedbackTypeError;   break;
        }
        UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init];
        [g prepare];
        [g notificationOccurred:t];
    }
}
