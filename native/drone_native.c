#include "drone_native.h"
#include <limits.h>
// Real integration: integer distance-weighted operational cost (3 units/km).
// Negative values or an overflow produce the documented failure sentinel -1.
int calculate_surcharge(int base_fee, int rounded_distance_km) {
    if (base_fee < 0 || rounded_distance_km < 0) return -1;
    if (rounded_distance_km > (INT_MAX - base_fee) / 3) return -1;
    return base_fee + rounded_distance_km * 3;
}
