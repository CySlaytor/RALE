#pragma once

#include <QDialog>
#include <QTextEdit>
#include <QLabel>
#include <QStringList>
#include <memory>
#include "../../Models/AssetModels.h"
#include "../../Models/LogicModels.h"

namespace RALogicEditor {

class AssetDiffDialog : public QDialog
{
    Q_OBJECT

public:
    AssetDiffDialog(
        const std::shared_ptr<IAssetItem> &serverItem,
        const std::shared_ptr<IAssetItem> &localItem,
        QWidget *parent = nullptr);

private:
    QTextEdit *rtbServer = nullptr;
    QTextEdit *rtbLocal = nullptr;
    QLabel *lblServer = nullptr;
    QLabel *lblLocal = nullptr;
    QLabel *lblStats = nullptr;

    void InitializeUI();
    QStringList GetReadableLines(const QString &rawLogic, bool isLeaderboard);
    QString ConditionToAlignedString(const AchievementCondition &cond);
    QString OperandToAligned(const Operand &op);
    void AlignDiffLists(QStringList &server, QStringList &local);
    void HighlightDifferences(const QStringList &serverLines, const QStringList &localLines);
    void RenderBox(QTextEdit *box, const QStringList &sourceLines, const QStringList &compareLines, const QColor &diffBack, const QColor &metaColor, bool isServer);
    void UpdateDiffStats(const QStringList &serverLines, const QStringList &localLines);
};

} // namespace RALogicEditor
