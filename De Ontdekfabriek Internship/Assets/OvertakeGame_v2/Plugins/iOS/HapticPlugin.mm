// HapticPlugin.mm
// Native iOS plugin for Unity — accesses the Taptic Engine directly.
// Place in Assets/OvertakeGame/Plugins/iOS/
// Unity will compile this automatically when building for iOS.

#import <UIKit/UIKit.h>

extern "C" {

    // Impact haptic: style 0=Light, 1=Medium, 2=Heavy
    void _TriggerImpactHaptic(int style) {
        UIImpactFeedbackStyle feedbackStyle;
        switch(style) {
            case 0:  feedbackStyle = UIImpactFeedbackStyleLight;  break;
            case 2:  feedbackStyle = UIImpactFeedbackStyleHeavy;  break;
            default: feedbackStyle = UIImpactFeedbackStyleMedium; break;
        }
        UIImpactFeedbackGenerator *generator =
            [[UIImpactFeedbackGenerator alloc] initWithStyle:feedbackStyle];
        [generator prepare];
        [generator impactOccurred];
    }

    // Notification haptic: type 0=Success, 1=Warning, 2=Error
    void _TriggerNotificationHaptic(int type) {
        UINotificationFeedbackType feedbackType;
        switch(type) {
            case 0:  feedbackType = UINotificationFeedbackTypeSuccess; break;
            case 1:  feedbackType = UINotificationFeedbackTypeWarning; break;
            default: feedbackType = UINotificationFeedbackTypeError;   break;
        }
        UINotificationFeedbackGenerator *generator =
            [[UINotificationFeedbackGenerator alloc] init];
        [generator prepare];
        [generator notificationOccurred:feedbackType];
    }
}
