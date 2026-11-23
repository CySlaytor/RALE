#pragma once

#include <QString>

#include <cstdint>

namespace RALE {

struct NoteTemplateInfo {
    uint32_t DetectedStride = 64;
    int BaseIndex = 1;
    QString HeaderTemplate = "Structure {INDEX:02}";
    QString FirstCustomName;
    QString BodyText;
    QString PrefixTag;
};

class CodeNoteTemplateService {
  public:
    static NoteTemplateInfo ParseTemplateFromNote(const QString& noteText);
    static QString ExpandTemplate(const QString& headerTemplate, int indexNumber, uint32_t offsetBytes, const QString& customName);
};

} // namespace RALE
