#pragma once

#include "../Models/LogicModels.h"
#include "../Models/NoteNode.h"

#include <QList>
#include <QString>

#include <memory>
#include <tuple>

namespace RALE {

// Interface for parsing Code Notes into tree structures and generating logic.
// Enables Dependency Injection for ViewModels.
class IPointerTreeParser {
  public:
    virtual ~IPointerTreeParser() = default;

    virtual QList<std::shared_ptr<NoteNode>> ParseNoteText(const QString& noteText) = 0;
    virtual std::tuple<QString, QString, QString>
    GeneratePointerLogic(const CodeNote& baseNote, const std::shared_ptr<NoteNode>& selectedNode, int consoleId = 0) = 0;
    virtual QList<int64_t> GetOffsetsFromNode(const std::shared_ptr<NoteNode>& node) = 0;
};

} // namespace RALE
