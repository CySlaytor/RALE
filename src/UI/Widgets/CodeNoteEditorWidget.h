#pragma once

#include "../../Models/LogicModels.h"
#include "../../Models/NoteNode.h"
#include "../Models/CodeNoteViewModel.h"

#include <QCheckBox>
#include <QComboBox>
#include <QLabel>
#include <QLineEdit>
#include <QMenu>
#include <QPlainTextEdit>
#include <QPushButton>
#include <QWidget>

#include <memory>

namespace RALE {

class CodeNoteEditorWidget : public QWidget {
    Q_OBJECT

  public:
    explicit CodeNoteEditorWidget(QWidget* parent = nullptr);

    bool ShowLeafsOnly() const;
    void SetShowLeafsOnly(bool value);

    bool IndentValues() const;
    void SetIndentValues(bool value);

    bool UnicodeTree() const;
    void SetUnicodeTree(bool value);

    void SetConsoleId(int consoleId);

    void SetNotes(const QList<CodeNote>& notes);
    void CommitChanges();

    void HighlightAddress(const QString& address);
    void HighlightVariable(const QList<int64_t>& offsets);
    std::tuple<QString, QString, QString> GetSelectedLogic();

    const QList<CodeNote>& GetAllNotes() const { return _viewModel->GetAllNotes(); }

    bool CanUndo() const { return _viewModel->CanUndo(); }
    bool CanRedo() const { return _viewModel->CanRedo(); }
    void Undo();
    void Redo();

    void ExecuteBatchMultiply(const QList<CodeNote>& selectedNotes, const QString& searchKeyword, const QString& replacePattern, int copies,
                              int64_t stride, int startNum);

    void updateThemeIcons();

  signals:
    void ReloadRequested();
    void SaveRequested();
    void NoteUpdated();
    void NoteDirtyStateChanged();
    void BeforeBatchNotesChange();
    void BatchNotesApplied();
    void NoteResetRequested(const QString& address);

  protected:
    bool eventFilter(QObject* watched, QEvent* event) override;

  private slots:
    void onSearchTextChanged(const QString& text);
    void onNoteIndexChanged(int index);
    void onVariableIndexChanged(int index);
    void onNoteBodyTextChanged();
    void onBtnReloadClicked();
    void onBtnCloneStrideClicked();
    void onBtnSaveClicked();
    void onBtnResetClicked();
    void onBtnFixHierarchyClicked();

    void onVmNoteContentChanged();
    void onVmEditStateChanged();
    void onVmSelectionRequested(std::shared_ptr<NoteNode> node);

    void onCutClicked();
    void onCopyClicked();
    void onPasteClicked();
    void onDeleteNodeClicked();

  private:
    QLineEdit* txtSearch = nullptr;
    QPushButton* btnReload = nullptr;
    QComboBox* cmbNotes = nullptr;
    QPushButton* btnCloneStride = nullptr;
    QComboBox* cmbVariables = nullptr;
    QCheckBox* chkShowLeafsOnly = nullptr;
    QPlainTextEdit* txtNoteBody = nullptr;

    QPushButton* btnSave = nullptr;
    QPushButton* btnFixHierarchy = nullptr;
    QCheckBox* chkUnicodeTree = nullptr;
    QCheckBox* chkIndentValues = nullptr;
    QPushButton* btnReset = nullptr;

    QLabel* lblOffsetChain = nullptr;
    QMenu* cmsNoteContext = nullptr;

    std::unique_ptr<CodeNoteViewModel> _viewModel;
    QList<CodeNote> _filteredNotes;
    QString _lastVariableTreeAddress;
    int _consoleId = 0;
    bool _isInternalUpdate = false;
    bool _isRefreshingVariables = false;
    bool _variableRefreshPending = false;
    bool _hasUnsavedChanges = false;

    QIcon getThemeIcon(const QString& resourcePath) const;
    void InitializeUI();
    void ApplyFilter(bool autoSelectFirst = true);
    void RefreshVariableTreeDisplay();
    void UpdateStatusDisplay(const QString* overrideMsg = nullptr);
};

} // namespace RALE
