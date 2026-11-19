#pragma once

#include <QString>
#include "../Models/RichPresence.h"

namespace RALE {

class RichPresenceParser
{
public:
    static RichPresenceScript Parse(const QString &content);
};

} // namespace RALE
