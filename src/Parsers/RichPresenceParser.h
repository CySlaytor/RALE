#pragma once

#include "../Models/RichPresence.h"

#include <QString>

namespace RALE {

class RichPresenceParser {
  public:
    static RichPresenceScript Parse(const QString& content);
};

} // namespace RALE
