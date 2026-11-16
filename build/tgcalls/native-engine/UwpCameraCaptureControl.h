#pragma once

#include <string>

namespace Unigram {
namespace Native {
namespace Calls {

void SetCameraDeviceOrientationHint(const std::string& deviceSelector, bool isFrontCamera);
bool IsFrontCameraDevice(const std::string& deviceSelector);
void SetCameraCaptureInflightStage(const char* stage);
void ClearCameraCaptureInflightStage();
// Privacy-safe camera timing and frame counters for the live capturer, as ";key=value"
// pairs; empty when no capturer exists.
std::string DescribeCameraCaptureStatus();

class UwpCameraCaptureControl {
public:
    virtual ~UwpCameraCaptureControl() = default;

    virtual bool SwitchToDevice(std::string deviceSelector, bool isFrontCamera) = 0;
};

}
}
}
