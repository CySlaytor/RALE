#pragma once

#include <QString>
#include "../Models/RichPresence.h"

namespace RALogicEditor {

class RichPresenceParser
{
public:
    static RichPresenceScript Parse(const QString &content);
};

} // namespace RALogicEditor
