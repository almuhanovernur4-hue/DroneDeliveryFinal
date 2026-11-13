#ifndef DRONE_NATIVE_H
#define DRONE_NATIVE_H
#ifdef _WIN32
#define DRONE_EXPORT __declspec(dllexport)
#else
#define DRONE_EXPORT __attribute__((visibility("default")))
#endif
#ifdef __cplusplus
extern "C" {
#endif
DRONE_EXPORT int calculate_surcharge(int base_fee, int rounded_distance_km);
#ifdef __cplusplus
}
#endif
#endif
