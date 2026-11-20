#pragma once

#include "../../Models/AssetModels.h"
#include "../../Models/LogicModels.h"

#include <QDialog>
#include <QLabel>
#include <QStringList>
#include <QTextEdit>

#include <memory>

namespace RALE {

class AssetDiffDialog : public QDialog {
    Q_OBJECT

  public:
    AssetDiffDialog(const std::shared_ptr<IAssetItem>& serverItem, const std::shared_ptr<IAssetItem>& localItem, QWidget* parent = nullptr);

  protected:
    void done(int r) override;

  private:
    QTextEdit* rtbServer = nullptr;
    QTextEdit* rtbLocal = nullptr;
    QLabel* lblServer = nullptr;
    QLabel* lblLocal = nullptr;
    QLabel* lblStats = nullptr;

    void InitializeUI();
    void loadPositionSettings();
    void savePositionSettings();
    QStringList GetReadableLines(const QString& rawLogic, bool isLeaderboard);
    QString ConditionToAlignedString(const AchievementCondition& cond);
    QString OperandToAligned(const Operand& op);
    void AlignDiffLists(QStringList& server, QStringList& local);
    void HighlightDifferences(const QStringList& serverLines, const QStringList& localLines);
    void RenderBox(QTextEdit* box, const QStringList& sourceLines, const QStringList& compareLines, const QColor& diffBack,
                   const QColor& metaColor);
    void UpdateDiffStats(const QStringList& serverLines, const QStringList& localLines);
};

} // namespace RALE
