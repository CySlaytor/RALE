#pragma once

#include "ILogicValidator.h"

namespace RALE {

class LogicValidator : public ILogicValidator {
  public:
    struct ParsedCodeNote {
        int64_t Addr = -1;
        QString AddressHex;
        QString Note;
        QString AddressNote;
        bool HasGeneralGameNotes = false;
        int64_t Size = 1;
        QString TypeName;
        uint64_t TypeMaxValue = 0;
        bool HasInfiniteMaxValue = false;
        bool ProbablePointer = false;

        struct EnumEntry {
            QString Literal;
            double Value = 0.0;
            bool IsInteger = true;
            QString Meaning;
        };
        QList<EnumEntry> Enumerations;

        bool IsGeneralGameNotesOnly() const { return HasGeneralGameNotes && AddressNote.trimmed().isEmpty(); }
        bool Contains(int64_t addr) const { return addr >= Addr && addr < Addr + Size; }
        QString Header() const;
    };

    static ParsedCodeNote ParseCodeNote(const CodeNote& note);
    static bool NeedsFeedback(const IAssetItem* asset);

    // Validates the entire asset (all groups) for cross-group logic like Delta checks
    QList<ValidationIssue> ValidateAsset(const QList<AchievementConditionGroup>& groups, std::shared_ptr<IAssetItem> asset,
                                         const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup = nullptr,
                                         const QList<ConditionFormatter::RangeEntry>* rangeCache = nullptr,
                                         const QList<CodeNote>* rawNotes = nullptr) override;

    QList<ValidationIssue> ValidateLeaderboard(const QMap<QString, QList<AchievementConditionGroup>>& segments,
                                               std::shared_ptr<IAssetItem> asset,
                                               const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup = nullptr,
                                               const QList<ConditionFormatter::RangeEntry>* rangeCache = nullptr,
                                               const QList<CodeNote>* rawNotes = nullptr) override;

    QList<ValidationIssue> ValidateCodeNotes(const QList<CodeNote>& notes, int consoleId = 0) override;

    QList<ValidationIssue> ValidateSet(const QList<std::shared_ptr<IAssetItem>>& allAssets) override;

    // Fast path for validating a full set of notes once before validating many assets
    static QList<ParsedCodeNote> BuildParsedNotes(const QList<CodeNote>& rawNotes);

    QList<ValidationIssue> ValidateAchievementWithParsedNotes(const QList<AchievementConditionGroup>& groups,
                                                              std::shared_ptr<IAssetItem> asset,
                                                              const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                                                              const QList<ConditionFormatter::RangeEntry>* rangeCache,
                                                              const QList<ParsedCodeNote>& parsedNotes);

    QList<ValidationIssue> ValidateLeaderboardWithParsedNotes(const QMap<QString, QList<AchievementConditionGroup>>& segments,
                                                              std::shared_ptr<IAssetItem> asset,
                                                              const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                                                              const QList<ConditionFormatter::RangeEntry>* rangeCache,
                                                              const QList<ParsedCodeNote>& parsedNotes);

  private:
    QList<ValidationIssue> ValidateLogicBlock(const QList<AchievementConditionGroup>& groups, bool isValue, bool runFullLogicTests,
                                              std::shared_ptr<IAssetItem> asset, const QString& segmentName,
                                              const QMap<QString, QList<std::shared_ptr<NoteNode>>>* notesLookup,
                                              const QList<ConditionFormatter::RangeEntry>* rangeCache,
                                              const QList<ParsedCodeNote>& parsedNotes);

    void CheckDeltas(const QList<AchievementConditionGroup>& groups, QList<ValidationIssue>& issues, std::shared_ptr<IAssetItem> asset,
                     const QString& segmentName);
    void CheckOneCondition(const QList<AchievementConditionGroup>& groups, bool isValue, QList<ValidationIssue>& issues,
                           std::shared_ptr<IAssetItem> asset, const QString& segmentName);

    // Check for undocumented memory addresses (Missing Code Notes)
    void CheckMissingNotes(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                           std::shared_ptr<IAssetItem> asset, const QString& segmentName,
                           const QMap<QString, QList<std::shared_ptr<NoteNode>>>& notesLookup,
                           const QList<ConditionFormatter::RangeEntry>* rangeCache, const QList<ParsedCodeNote>& parsedNotes);

    void CheckMismatchNotes(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                            std::shared_ptr<IAssetItem> asset, const QString& segmentName, const QList<ParsedCodeNote>& parsedNotes);
    void CheckPointers(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                       std::shared_ptr<IAssetItem> asset, const QString& segmentName, const QList<ParsedCodeNote>& parsedNotes);
    void CheckValidOffsets(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                           std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckBadChains(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                        std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckPriors(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                     std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckStaleAddAddress(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                              std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckUnclearedHits(const QList<AchievementConditionGroup>& groups, bool isValue, QList<ValidationIssue>& issues,
                            std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckUselessAndNext(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                             std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckPauseLocks(const QList<AchievementConditionGroup>& groups, QList<ValidationIssue>& issues,
                         std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckUselessPause(const QList<AchievementConditionGroup>& groups, bool isValue, QList<ValidationIssue>& issues,
                           std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckUselessReset(const QList<AchievementConditionGroup>& groups, bool isValue, QList<ValidationIssue>& issues,
                           std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckResetWithHits(const AchievementConditionGroup& group, int groupIndex, QList<ValidationIssue>& issues,
                            std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckUselessResetNextIf(const AchievementConditionGroup& group, int groupIndex, bool isValue, QList<ValidationIssue>& issues,
                                 std::shared_ptr<IAssetItem> asset, const QString& segmentName);
    void CheckUselessAddHits(const AchievementConditionGroup& group, int groupIndex, bool isValue, QList<ValidationIssue>& issues,
                             std::shared_ptr<IAssetItem> asset, const QString& segmentName);

    void CheckPresentation(std::shared_ptr<IAssetItem> asset, QList<ValidationIssue>& issues);
    void CheckTitleCase(std::shared_ptr<IAssetItem> asset, QList<ValidationIssue>& issues);
    void CheckWritingMistakes(std::shared_ptr<IAssetItem> asset, QList<ValidationIssue>& issues);
    void CheckBrackets(std::shared_ptr<IAssetItem> asset, QList<ValidationIssue>& issues);

    static bool IsChainFlag(const QString& flag);
};

} // namespace RALE
