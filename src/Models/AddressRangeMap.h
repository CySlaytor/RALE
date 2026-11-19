#pragma once

#include <QString>
#include <cstdint>

namespace RALE {

class AddressRangeMap
{
public:
    QString Region = "";
    int64_t SourceBase = 0;
    int64_t TargetBase = 0;
    int64_t Size = 0; // Bytes

    bool IsInRange(int64_t address) const
    {
        return address >= SourceBase && address < SourceBase + Size;
    }

    int64_t CalculateTarget(int64_t address) const
    {
        int64_t offset = address - SourceBase;
        return TargetBase + offset;
    }
};

} // namespace RALE
