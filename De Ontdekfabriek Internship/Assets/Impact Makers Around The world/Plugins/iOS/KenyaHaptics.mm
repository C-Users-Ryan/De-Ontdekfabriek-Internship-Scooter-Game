// Native iOS impact haptics for Kenya Scooter.
// Exposes a C entry point the Unity C# HapticDriver calls via [DllImport("__Internal")].
// On iOS 13+ the impact is fired WITH an intensity (0..1) so a harder game hit buzzes harder;
// iOS 10-12 fire a fixed heavy impact; older still falls back to the system vibrate.
#import <UIKit/UIKit.h>
#import <AudioToolbox/AudioToolbox.h>

extern "C" {

void _KenyaHapticImpact(float intensity) {
    if (intensity < 0.0f) intensity = 0.0f;
    if (intensity > 1.0f) intensity = 1.0f;

    if (@available(iOS 10.0, *)) {
        UIImpactFeedbackGenerator *generator =
            [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
        [generator prepare];
        if (@available(iOS 13.0, *)) {
            [generator impactOccurredWithIntensity:(CGFloat)intensity];
        } else {
            [generator impactOccurred];
        }
    } else {
        AudioServicesPlaySystemSound(kSystemSoundID_Vibrate);
    }
}

bool _KenyaHapticSupported() {
    if (@available(iOS 10.0, *)) { return true; }
    return false;
}

}
