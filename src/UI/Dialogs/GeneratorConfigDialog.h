#pragma once

#include "../../Models/GeneratorModels.h"
#include "../../Models/LogicModels.h"

#include <QCheckBox>
#include <QComboBox>
#include <QDialog>
#include <QLineEdit>
#include <QMap>
#include <QPushButton>
#include <QSet>
#include <QStringList>
#include <QTableWidget>
#include <QTreeWidget>
#include <QWidget>

#include <memory>

namespace RALE {

// A dialog to capture Copies, Stride, and select multiple Addresses for the Logic Generator
class GeneratorConfigDialog : public QDialog {
    Q_OBJECT

  public:
    // ViewModel to handle hierarchy and display formatting
    class GeneratorItemViewModel {
      public:
        QString Address = "";
        QString Description = "";
        QList<std::shared_ptr<GeneratorItemViewModel>> Children;

        QString ToString() const;
    };

    struct NoteOverrideConfig {
        QString HeaderTemplate;
        QMap<int, QString> CustomNames;
        QMap<int, QString> ExplicitTitles;
    };

    int Copies = 1;
    int Stride = 0;
    bool IsArray = false;
    bool DuplicateSelectedOnly = false;
    bool DuplicateNotes = false;

    // List of addresses the user selected to apply the generator rule to
    QStringList SelectedAddresses;

    // Per-address note template and token overrides (keyed by normalized address)
    QMap<QString, NoteOverrideConfig> NoteOverrides;

    // Constructor now accepts a hierarchical list of items and pre-selected addresses
    GeneratorConfigDialog(const QList<std::shared_ptr<GeneratorItemViewModel>>& rootItems, int defaultStride = 0,
                          const GeneratorDefinition* existingDef = nullptr, const QSet<QString>* preSelectedAddresses = nullptr,
                          QWidget* parent = nullptr, const QList<CodeNote>* notes = nullptr,
                          const AchievementConditionGroup* sourceGroup = nullptr);

  protected:
    void done(int r) override;

  private slots:
    void onTreeItemChanged(QTreeWidgetItem* item, int column);
    void onSelectAll();
    void onBtnOK();
    void onBtnCancel();
    void onDuplicateNotesToggled(bool checked);
    void onNoteSourceChanged(int index);
    void onNoteTemplateChanged(const QString& text);
    void onPasteNamesClicked();
    void onNotePreviewItemChanged(QTableWidgetItem* item);
    void onParametersChanged();

  private:
    // Static field to track if user explicitly customized stride this session
    static bool _userModifiedStride; // Tracks if the user has explicitly set a stride this session

    const QList<CodeNote>* _notes = nullptr;
    const AchievementConditionGroup* _sourceGroup = nullptr;

    QTreeWidget* tvAddresses = nullptr;
    QLineEdit* txtCopies = nullptr;
    QLineEdit* txtStride = nullptr;
    QCheckBox* chkGenerateAsArray = nullptr;
    QCheckBox* chkDuplicateSelectedOnly = nullptr;
    QCheckBox* chkDuplicateNotes = nullptr;
    QPushButton* btnSelectAll = nullptr;
    QPushButton* btnOK = nullptr;
    QPushButton* btnCancel = nullptr;

    // Right-side Note Template & Preview Panel (visible when DuplicateNotes is checked)
    QWidget* _notePreviewPanel = nullptr;
    QComboBox* _cmbNoteSource = nullptr;
    QLineEdit* _txtNoteTemplate = nullptr;
    QPushButton* _btnPasteNames = nullptr;
    QTableWidget* _tblNotePreview = nullptr;

    struct ResolvedSourceNote {
        QString NormAddress;
        QString DisplayLabel;
        QString SourceHeaderText;
        int BaseIndex = 1;
        bool IsStatic = true;
        QString BaseNoteKey;
        QList<int64_t> ChainOffsets;
        int ChangedLevel = -1;
        int64_t SourceValue = 0;
    };

    QList<ResolvedSourceNote> _resolvedSources;
    bool _isUpdatingCheck = false;
    bool _isUpdatingNotePreview = false;

    void InitializeUI();
    void loadPositionSettings();
    void savePositionSettings();
    void InitializeTreeView(const QList<std::shared_ptr<GeneratorItemViewModel>>& items);
    QTreeWidgetItem* CreateTreeNode(const GeneratorItemViewModel& item);
    void CheckNodesRecursive(QTreeWidgetItem* item, const QSet<QString>& addressesToCheck);
    void SetChildrenChecked(QTreeWidgetItem* item, bool isChecked);
    void CollectCheckedAddresses(QTreeWidgetItem* item);
    bool IsNodeTreeChecked(QTreeWidgetItem* item) const;

    void UpdatePreviewPanelVisibility(bool visible);
    void RefreshNoteSourceCombo();
    void RebuildNotePreviewTable();
    bool ParseCopiesAndStride(int& outCopies, int& outStride) const;
    ResolvedSourceNote ResolveSourceInfoForAddress(const QString& addr) const;
    QString CheckTargetStatus(const ResolvedSourceNote& src, int copyIndex, int stride, QString& outExistingTitle) const;
};

} // namespace RALE
