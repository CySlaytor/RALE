#include "CodeNoteEditorWidget.h"

#include "../../Common/AppSettings.h"
#include "../../Logic/CodeNoteLogic.h"
#include "../../Parsers/PointerTreeParser.h"
#include "../../Utilities/LogicFormatter.h"
#include "../Dialogs/CloneStrideDialog.h"
#include "../Styles/ThemeManager.h"

#include <QGuiApplication>
#include <QHBoxLayout>
#include <QKeyEvent>
#include <QMessageBox>
#include <QRegularExpression>
#include <QSignalBlocker>
#include <QVBoxLayout>

namespace RALE {

namespace {

QString NormalizeLineEndings(QString text) {
    text.replace("\r\n", "\n").replace('\r', '\n');
    return text;
}

} // namespace

CodeNoteEditorWidget::CodeNoteEditorWidget(QWidget* parent) : QWidget(parent) {
    static PointerTreeParser staticParser;
    _viewModel = std::make_unique<CodeNoteViewModel>(staticParser, this);

    InitializeUI();

    connect(_viewModel.get(), &CodeNoteViewModel::NotesLoaded, this, [this]() { ApplyFilter(false); });
    connect(_viewModel.get(), &CodeNoteViewModel::NoteContentChanged, this, &CodeNoteEditorWidget::onVmNoteContentChanged);
    connect(_viewModel.get(), &CodeNoteViewModel::EditStateChanged, this, &CodeNoteEditorWidget::onVmEditStateChanged);
    connect(_viewModel.get(), &CodeNoteViewModel::SelectionRequested, this, &CodeNoteEditorWidget::onVmSelectionRequested);
    connect(_viewModel.get(), &CodeNoteViewModel::NoteContentChanged, this, &CodeNoteEditorWidget::NoteUpdated);
    connect(_viewModel.get(), &CodeNoteViewModel::HistoryChanged, this, &CodeNoteEditorWidget::HistoryChanged);

    connect(chkShowLeafsOnly, &QCheckBox::toggled, this, &CodeNoteEditorWidget::RefreshVariableTreeDisplay);

    chkUnicodeTree->setChecked(AppSettings::Current.CodeNoteUnicodeTree);
    chkIndentValues->setChecked(AppSettings::Current.CodeNoteIndentDescriptions);

    connect(chkUnicodeTree, &QCheckBox::toggled, this, [this](bool checked) {
        AppSettings::Current.CodeNoteUnicodeTree = checked;
        AppSettings::Save();
    });

    connect(chkIndentValues, &QCheckBox::toggled, this, [this](bool checked) {
        AppSettings::Current.CodeNoteIndentDescriptions = checked;
        AppSettings::Save();
    });
}

QIcon CodeNoteEditorWidget::getThemeIcon(const QString& resourcePath) const {
    return ThemeManager::getIcon(resourcePath, ThemeManager::CurrentTheme());
}

void CodeNoteEditorWidget::updateThemeIcons() {
    if (!cmsNoteContext)
        return;
    const auto actionsList = cmsNoteContext->actions();
    for (QAction* action : actionsList) {
        QVariant iconPathVar = action->property("iconPath");
        if (iconPathVar.isValid()) {
            action->setIcon(getThemeIcon(iconPathVar.toString()));
        }
    }
}

void CodeNoteEditorWidget::InitializeUI() {
    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(0, 0, 0, 0);
    mainLayout->setSpacing(0);

    auto* panelTop = new QWidget(this);
    auto* topLayout = new QVBoxLayout(panelTop);
    topLayout->setContentsMargins(4, 4, 4, 2);
    topLayout->setSpacing(4);

    // Row 1: Search
    auto* rowSearch = new QHBoxLayout();
    rowSearch->setContentsMargins(0, 0, 0, 0);
    rowSearch->setSpacing(4);
    auto* lblSearch = new QLabel("Search: ", panelTop);
    lblSearch->setFixedWidth(52);
    rowSearch->addWidget(lblSearch);
    txtSearch = new QLineEdit(panelTop);
    txtSearch->setFixedHeight(23);
    connect(txtSearch, &QLineEdit::textChanged, this, &CodeNoteEditorWidget::onSearchTextChanged);
    rowSearch->addWidget(txtSearch, 1);
    btnReload = new QPushButton("Reload", panelTop);
    btnReload->setFixedSize(60, 23);
    connect(btnReload, &QPushButton::clicked, this, &CodeNoteEditorWidget::onBtnReloadClicked);
    rowSearch->addWidget(btnReload);
    topLayout->addLayout(rowSearch);

    // Row 2: Address
    auto* rowAddress = new QHBoxLayout();
    rowAddress->setContentsMargins(0, 0, 0, 0);
    rowAddress->setSpacing(4);
    auto* lblAddress = new QLabel("Address: ", panelTop);
    lblAddress->setFixedWidth(52);
    rowAddress->addWidget(lblAddress);
    cmbNotes = new QComboBox(panelTop);
    ThemeManager::SetupComboBox(cmbNotes, 14);
    connect(cmbNotes, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &CodeNoteEditorWidget::onNoteIndexChanged);
    rowAddress->addWidget(cmbNotes, 1);
    btnCloneStride = new QPushButton("Clone Stride...", panelTop);
    btnCloneStride->setToolTip("Batch clone this structure across sequential memory strides (e.g. Levels, Slots)");
    btnCloneStride->setFixedHeight(26);
    btnCloneStride->setEnabled(false);
    connect(btnCloneStride, &QPushButton::clicked, this, &CodeNoteEditorWidget::onBtnCloneStrideClicked);
    rowAddress->addWidget(btnCloneStride);
    topLayout->addLayout(rowAddress);

    // Row 3: Variable
    auto* rowVariable = new QHBoxLayout();
    rowVariable->setContentsMargins(0, 0, 0, 0);
    rowVariable->setSpacing(4);
    auto* lblVariable = new QLabel("Variable: ", panelTop);
    lblVariable->setFixedWidth(52);
    rowVariable->addWidget(lblVariable);
    cmbVariables = new QComboBox(panelTop);
    ThemeManager::SetupComboBox(cmbVariables, 14);
    connect(cmbVariables, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &CodeNoteEditorWidget::onVariableIndexChanged);
    rowVariable->addWidget(cmbVariables, 1);
    chkShowLeafsOnly = new QCheckBox("Show Leafs Only", panelTop);
    rowVariable->addWidget(chkShowLeafsOnly);
    topLayout->addLayout(rowVariable);

    mainLayout->addWidget(panelTop);

    // Text Editor
    txtNoteBody = new QPlainTextEdit(this);
    txtNoteBody->setObjectName("txtNoteBody");
    txtNoteBody->setFont(QFont("Consolas", 10));
    txtNoteBody->installEventFilter(this);
    connect(txtNoteBody, &QPlainTextEdit::textChanged, this, &CodeNoteEditorWidget::onNoteBodyTextChanged);
    connect(txtNoteBody->document(), &QTextDocument::undoAvailable, this, [this](bool) { emit HistoryChanged(); });
    connect(txtNoteBody->document(), &QTextDocument::redoAvailable, this, [this](bool) { emit HistoryChanged(); });

    cmsNoteContext = new QMenu(this);
    auto* actCut = cmsNoteContext->addAction(getThemeIcon(":/icons/cut.svg"), "Cut", this, &CodeNoteEditorWidget::onCutClicked);
    actCut->setShortcut(QKeySequence::Cut);
    actCut->setShortcutContext(Qt::WidgetShortcut);
    actCut->setProperty("iconPath", ":/icons/cut.svg");

    auto* actCopy = cmsNoteContext->addAction(getThemeIcon(":/icons/copy.svg"), "Copy", this, &CodeNoteEditorWidget::onCopyClicked);
    actCopy->setShortcut(QKeySequence::Copy);
    actCopy->setShortcutContext(Qt::WidgetShortcut);
    actCopy->setProperty("iconPath", ":/icons/copy.svg");

    auto* actPaste = cmsNoteContext->addAction(getThemeIcon(":/icons/paste.svg"), "Paste", this, &CodeNoteEditorWidget::onPasteClicked);
    actPaste->setShortcut(QKeySequence::Paste);
    actPaste->setShortcutContext(Qt::WidgetShortcut);
    actPaste->setProperty("iconPath", ":/icons/paste.svg");

    txtNoteBody->addActions({actCut, actCopy, actPaste});

    cmsNoteContext->addSeparator();
    auto* actDelNode = cmsNoteContext->addAction(getThemeIcon(":/icons/delete.svg"), "Delete Note (Selected in Tree)", this,
                                                 &CodeNoteEditorWidget::onDeleteNodeClicked);
    actDelNode->setProperty("iconPath", ":/icons/delete.svg");

    cmsNoteContext->addSeparator();
    auto* actRevertNote =
        cmsNoteContext->addAction(getThemeIcon(":/icons/undo.svg"), "Revert to Server", this, &CodeNoteEditorWidget::onBtnRevertClicked);
    actRevertNote->setProperty("iconPath", ":/icons/undo.svg");

    auto* actResetNote =
        cmsNoteContext->addAction(getThemeIcon(":/icons/refresh.svg"), "Reset", this, &CodeNoteEditorWidget::onBtnResetClicked);
    actResetNote->setProperty("iconPath", ":/icons/refresh.svg");

    connect(cmsNoteContext, &QMenu::aboutToShow, this, [this, actDelNode, actRevertNote, actResetNote]() {
        actDelNode->setEnabled(!_viewModel->EditState.IsVirtual);
        actRevertNote->setEnabled(CanRevertCurrentNote());
        actResetNote->setEnabled(CanResetCurrentNote());
    });

    txtNoteBody->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(txtNoteBody, &QWidget::customContextMenuRequested, this,
            [this](const QPoint& pos) { cmsNoteContext->exec(txtNoteBody->mapToGlobal(pos)); });

    mainLayout->addWidget(txtNoteBody, 1);

    // Panel Bottom (Button toolbar: fixed height 34px with comfortable margins to avoid border clipping)
    auto* panelBottom = new QWidget(this);
    panelBottom->setFixedHeight(34);
    auto* bottomLayout = new QHBoxLayout(panelBottom);
    bottomLayout->setContentsMargins(4, 4, 4, 4);
    bottomLayout->setSpacing(6);

    btnFixHierarchy = new QPushButton("Fix Hierarchy", panelBottom);
    btnFixHierarchy->setFixedSize(82, 26);
    connect(btnFixHierarchy, &QPushButton::clicked, this, &CodeNoteEditorWidget::onBtnFixHierarchyClicked);
    bottomLayout->addWidget(btnFixHierarchy);

    chkUnicodeTree = new QCheckBox("Unicode Tree", panelBottom);
    chkUnicodeTree->setToolTip("Use Unicode box-drawing characters (├─, └─, │) for tree hierarchy indentation");
    bottomLayout->addWidget(chkUnicodeTree);

    chkIndentValues = new QCheckBox("Indent Descriptions", panelBottom);
    chkIndentValues->setToolTip("Indent attached value descriptions and enumerations (using dots) to match the parent pointer's level");
    chkIndentValues->setChecked(true);
    bottomLayout->addWidget(chkIndentValues);

    bottomLayout->addStretch();

    btnRevert = new QPushButton("Revert", panelBottom);
    btnRevert->setFixedSize(54, 26);
    btnRevert->setToolTip("Discard local modifications and restore original values from the loaded JSON server snapshot");
    btnRevert->setEnabled(false);
    connect(btnRevert, &QPushButton::clicked, this, &CodeNoteEditorWidget::onBtnRevertClicked);
    bottomLayout->addWidget(btnRevert);

    btnReset = new QPushButton("Reset", panelBottom);
    btnReset->setFixedSize(54, 26);
    btnReset->setToolTip("Discard current unsaved changes and restore the last saved state");
    btnReset->setEnabled(false);
    connect(btnReset, &QPushButton::clicked, this, &CodeNoteEditorWidget::onBtnResetClicked);
    bottomLayout->addWidget(btnReset);

    mainLayout->addWidget(panelBottom);

    // Status Panel: Fixed 22px height container matching TriggerEditor's bottom bar with proper separation
    auto* panelStatus = new QWidget(this);
    panelStatus->setFixedHeight(22);
    auto* statusLayout = new QHBoxLayout(panelStatus);
    statusLayout->setContentsMargins(6, 2, 6, 2);
    statusLayout->setSpacing(0);

    lblOffsetChain = new QLabel("Ready", panelStatus);
    lblOffsetChain->setObjectName("lblOffsetChain");
    statusLayout->addWidget(lblOffsetChain);
    statusLayout->addStretch();

    mainLayout->addWidget(panelStatus);
}

bool CodeNoteEditorWidget::eventFilter(QObject* watched, QEvent* event) {
    if (watched == txtNoteBody) {
        if (event->type() == QEvent::FocusIn) {
            // Keep FocusIn lightweight
        } else if (event->type() == QEvent::KeyPress) {
            auto* ke = static_cast<QKeyEvent*>(event);
            if (ke->modifiers() == Qt::ControlModifier && ke->key() == Qt::Key_Z) {
                if (CanUndo()) {
                    Undo();
                    return true;
                }
            } else if ((ke->modifiers() == Qt::ControlModifier && ke->key() == Qt::Key_Y) ||
                       (ke->modifiers() == (Qt::ControlModifier | Qt::ShiftModifier) && ke->key() == Qt::Key_Z)) {
                if (CanRedo()) {
                    Redo();
                    return true;
                }
            }
        }
    }
    return QWidget::eventFilter(watched, event);
}

bool CodeNoteEditorWidget::CanUndo() const {
    if (txtNoteBody && txtNoteBody->document()->isUndoAvailable())
        return true;
    return _viewModel && _viewModel->CanUndo();
}

bool CodeNoteEditorWidget::CanRedo() const {
    if (txtNoteBody && txtNoteBody->document()->isRedoAvailable())
        return true;
    return _viewModel && _viewModel->CanRedo();
}

void CodeNoteEditorWidget::Undo() {
    if (txtNoteBody && txtNoteBody->hasFocus() && txtNoteBody->document()->isUndoAvailable()) {
        txtNoteBody->undo();
        emit HistoryChanged();
        return;
    }

    if (_viewModel->CanUndo()) {
        _viewModel->Undo();
        _hasUnsavedChanges = true;
        QString msg = "Undone.";
        UpdateStatusDisplay(&msg);
        UpdateActionButtons();
        emit HistoryChanged();
        return;
    }

    if (txtNoteBody && txtNoteBody->document()->isUndoAvailable()) {
        txtNoteBody->undo();
        emit HistoryChanged();
    }
}

void CodeNoteEditorWidget::Redo() {
    if (txtNoteBody && txtNoteBody->hasFocus() && txtNoteBody->document()->isRedoAvailable()) {
        txtNoteBody->redo();
        emit HistoryChanged();
        return;
    }

    if (_viewModel->CanRedo()) {
        _viewModel->Redo();
        _hasUnsavedChanges = true;
        QString msg = "Redone.";
        UpdateStatusDisplay(&msg);
        UpdateActionButtons();
        emit HistoryChanged();
        return;
    }

    if (txtNoteBody && txtNoteBody->document()->isRedoAvailable()) {
        txtNoteBody->redo();
        emit HistoryChanged();
    }
}

bool CodeNoteEditorWidget::ShowLeafsOnly() const { return chkShowLeafsOnly->isChecked(); }
void CodeNoteEditorWidget::SetShowLeafsOnly(bool value) { chkShowLeafsOnly->setChecked(value); }

bool CodeNoteEditorWidget::IndentValues() const { return chkIndentValues->isChecked(); }
void CodeNoteEditorWidget::SetIndentValues(bool value) { chkIndentValues->setChecked(value); }

bool CodeNoteEditorWidget::UnicodeTree() const { return chkUnicodeTree->isChecked(); }
void CodeNoteEditorWidget::SetUnicodeTree(bool value) { chkUnicodeTree->setChecked(value); }

void CodeNoteEditorWidget::SetConsoleId(int consoleId) {
    _consoleId = consoleId;
    if (_viewModel)
        _viewModel->ConsoleId = consoleId;
}

void CodeNoteEditorWidget::SetServerNotes(const QList<CodeNote>& serverNotes) {
    _serverNotesMap.clear();
    for (const auto& n : serverNotes) {
        const QString key = LogicFormatter::NormalizeAddress(n.Address);
        if (!_serverNotesMap.contains(key))
            _serverNotesMap.insert(key, n.Note);
    }
    UpdateActionButtons();
}

void CodeNoteEditorWidget::SetSavedNotes(const QList<CodeNote>& savedNotes) {
    _savedNotesMap.clear();
    for (const auto& n : savedNotes) {
        const QString key = LogicFormatter::NormalizeAddress(n.Address);
        _savedNotesMap.insert(key, n.Note);
    }
    UpdateActionButtons();
}

void CodeNoteEditorWidget::onBtnReloadClicked() { emit ReloadRequested(); }

void CodeNoteEditorWidget::OpenCloneStrideDialog() { onBtnCloneStrideClicked(); }

void CodeNoteEditorWidget::onBtnCloneStrideClicked() {
    if (_viewModel->CurrentBaseNote == nullptr) {
        QMessageBox::information(this, "Clone Stride", "Please select a Code Note first.");
        return;
    }

    if (!_viewModel->EditState.IsVirtual && !_viewModel->IsRedirected && _viewModel->EditState.IsDirty) {
        CommitChanges();
    }

    QString noteText = _viewModel->IsRedirected ? _viewModel->RedirectedContent : _viewModel->CurrentBaseNote->Note;

    CloneStrideDialog dialog(_viewModel->CurrentBaseNote->Address, noteText, this);
    if (dialog.exec() != QDialog::Accepted)
        return;

    auto generated = dialog.getGeneratedNotes();
    if (generated.isEmpty())
        return;

    emit BeforeBatchNotesChange();

    QList<CodeNote> notesToApply;
    notesToApply.reserve(generated.size());
    for (const auto& g : generated) {
        CodeNote n;
        n.Address = g.addressHex;
        n.Note = g.noteText;
        n.User = "Local";
        notesToApply.append(n);
    }

    int addedCount = 0;
    int updatedCount = 0;
    _viewModel->ApplyCloneStrideNotes(notesToApply, addedCount, updatedCount);

    // Switch to the next generated note in sequence so the developer can immediately inspect it
    QString nextInspectAddr = (generated.size() > 1) ? generated[1].addressHex : generated[0].addressHex;
    _viewModel->HighlightAddress(nextInspectAddr);

    if (_viewModel->CurrentBaseNote != nullptr) {
        bool wasUpdating = _isInternalUpdate;
        _isInternalUpdate = true;

        const QString targetNorm = LogicFormatter::NormalizeAddress(_viewModel->CurrentBaseNote->Address);
        bool found = false;
        for (int i = 0; i < _filteredNotes.size(); i++) {
            if (LogicFormatter::NormalizeAddress(_filteredNotes[i].Address) == targetNorm) {
                cmbNotes->setCurrentIndex(i);
                found = true;
                break;
            }
        }
        if (!found) {
            txtSearch->setText("");
            ApplyFilter(false);
            for (int i = 0; i < _filteredNotes.size(); i++) {
                if (LogicFormatter::NormalizeAddress(_filteredNotes[i].Address) == targetNorm) {
                    cmbNotes->setCurrentIndex(i);
                    break;
                }
            }
        }
        _isInternalUpdate = wasUpdating;
    }

    _hasUnsavedChanges = true;
    QString msg = QString("Batch cloned %1 note(s) (%2 new, %3 updated).").arg(generated.size()).arg(addedCount).arg(updatedCount);
    UpdateStatusDisplay(&msg);
    UpdateActionButtons();

    emit NoteUpdated();
    emit BatchNotesApplied();
    emit HistoryChanged();
}

void CodeNoteEditorWidget::onBtnRevertClicked() {
    if (!CanRevertCurrentNote() || _viewModel->CurrentBaseNote == nullptr)
        return;

    const QString addr = _viewModel->CurrentBaseNote->Address;

    QMessageBox msgBox(this);
    msgBox.setWindowTitle("Confirm Revert to Server");
    msgBox.setText(QString("Discard local changes and revert code note %1 to the server snapshot?").arg(addr));
    msgBox.setInformativeText("Warning: All current modifications and any local User.txt overrides for this code note will be "
                              "discarded, restoring the original values from the loaded JSON server snapshot.");
    msgBox.setIcon(QMessageBox::Warning);

    QPushButton* revertBtn = msgBox.addButton("Revert", QMessageBox::DestructiveRole);
    QPushButton* cancelBtn = msgBox.addButton("Cancel", QMessageBox::RejectRole);
    msgBox.setDefaultButton(cancelBtn);
    msgBox.exec();

    if (msgBox.clickedButton() == revertBtn) {
        emit NoteRevertRequested(addr);
    }
}

void CodeNoteEditorWidget::onBtnResetClicked() {
    if (!CanResetCurrentNote() || _viewModel->CurrentBaseNote == nullptr)
        return;

    const QString addr = _viewModel->CurrentBaseNote->Address;

    QMessageBox msgBox(this);
    msgBox.setWindowTitle("Confirm Code Note Reset");
    msgBox.setText(QString("Discard current unsaved changes and reset code note %1?").arg(addr));
    msgBox.setInformativeText("Any unsaved modifications made to this code note since the last load or save to User.txt will be "
                              "discarded, restoring its last saved state (User.txt override if saved, or server JSON baseline).");
    msgBox.setIcon(QMessageBox::Warning);

    QPushButton* resetBtn = msgBox.addButton("Reset", QMessageBox::DestructiveRole);
    QPushButton* cancelBtn = msgBox.addButton("Cancel", QMessageBox::RejectRole);
    msgBox.setDefaultButton(cancelBtn);
    msgBox.exec();

    if (msgBox.clickedButton() == resetBtn) {
        emit NoteResetRequested(addr);
    }
}

void CodeNoteEditorWidget::onBtnFixHierarchyClicked() {
    if (txtNoteBody->isReadOnly())
        return;

    QString currentText = txtNoteBody->toPlainText();
    if (currentText.trimmed().isEmpty())
        return;

    _viewModel->PushUndoState();

    QString fixedText = CodeNoteLogic::FixHierarchy(currentText, chkUnicodeTree->isChecked(), chkIndentValues->isChecked(), _consoleId);

    if (txtNoteBody->toPlainText() != fixedText) {
        txtNoteBody->setPlainText(fixedText);
        _viewModel->UpdateNoteContent(fixedText);
        _hasUnsavedChanges = true;
        QString msg = "Hierarchy fixed.";
        UpdateStatusDisplay(&msg);
        UpdateActionButtons();
        emit HistoryChanged();
    }
}

void CodeNoteEditorWidget::CommitChanges() {
    _viewModel->CommitEditState();
    // Synchronize local filtered notes to reflect committed content immediately
    if (_viewModel->CurrentBaseNote != nullptr) {
        const QString targetNorm = LogicFormatter::NormalizeAddress(_viewModel->CurrentBaseNote->Address);
        for (auto& fn : _filteredNotes) {
            if (LogicFormatter::NormalizeAddress(fn.Address) == targetNorm) {
                fn.Note = _viewModel->CurrentBaseNote->Note;
                break;
            }
        }
    }
}

void CodeNoteEditorWidget::SetNotes(const QList<CodeNote>& notes) {
    _isInternalUpdate = true;
    QString previousAddress = "";
    if (cmbNotes->currentIndex() >= 0 && cmbNotes->currentIndex() < _filteredNotes.size())
        previousAddress = _filteredNotes[cmbNotes->currentIndex()].Address;

    txtNoteBody->clear();
    _hasUnsavedChanges = false;
    _viewModel->ClearEditState();
    _viewModel->LoadNotes(notes);

    bool selectionRestored = false;
    if (!previousAddress.isEmpty()) {
        const QString prevNorm = LogicFormatter::NormalizeAddress(previousAddress);
        for (int i = 0; i < _filteredNotes.size(); i++) {
            if (LogicFormatter::NormalizeAddress(_filteredNotes[i].Address) == prevNorm) {
                cmbNotes->setCurrentIndex(i);
                _viewModel->SelectBaseNote(_filteredNotes[i]);
                selectionRestored = true;
                break;
            }
        }
    }

    if (!selectionRestored && !_filteredNotes.isEmpty()) {
        cmbNotes->setCurrentIndex(0);
        _viewModel->SelectBaseNote(_filteredNotes[0]);
    }
    btnCloneStride->setEnabled(_viewModel->CurrentBaseNote != nullptr && !_viewModel->AllNotes.isEmpty());
    UpdateStatusDisplay(nullptr);
    UpdateActionButtons();
    _isInternalUpdate = false;
    emit HistoryChanged();
}

void CodeNoteEditorWidget::ApplyFilter(bool autoSelectFirst) {
    QString filter = txtSearch->text().trimmed();
    if (filter.isEmpty()) {
        _filteredNotes = _viewModel->AllNotes;
    } else {
        _filteredNotes.clear();
        for (const auto& n : _viewModel->AllNotes) {
            if (n.Address.contains(filter, Qt::CaseInsensitive) || n.Note.contains(filter, Qt::CaseInsensitive))
                _filteredNotes.append(n);
        }
    }

    {
        QSignalBlocker blocker(cmbNotes);
        cmbNotes->clear();
        for (const auto& n : _filteredNotes) {
            QStringList lines = n.Note.split(QRegularExpression("[\r\n]+"), Qt::SkipEmptyParts);
            QString firstLine = lines.isEmpty() ? QString() : lines.first().trimmed();
            if (firstLine.length() > 50)
                firstLine = firstLine.left(47) + "...";
            cmbNotes->addItem(QString("%1 | %2").arg(n.Address, firstLine));
        }

        if (autoSelectFirst && !_filteredNotes.isEmpty()) {
            cmbNotes->setCurrentIndex(0);
        }
    }

    if (autoSelectFirst && !_filteredNotes.isEmpty()) {
        _viewModel->SelectBaseNote(_filteredNotes[0]);
    } else if (_filteredNotes.isEmpty()) {
        cmbVariables->clear();
        QString msg = "No matches found.";
        UpdateStatusDisplay(&msg);
    }

    btnCloneStride->setEnabled(_viewModel->CurrentBaseNote != nullptr && !_viewModel->AllNotes.isEmpty());
    UpdateActionButtons();
}

void CodeNoteEditorWidget::onSearchTextChanged(const QString& /*text*/) {
    if (!_isInternalUpdate)
        ApplyFilter(true);
}

void CodeNoteEditorWidget::onVmNoteContentChanged() {
    if (_viewModel->CurrentBaseNote == nullptr) {
        btnCloneStride->setEnabled(false);
        UpdateActionButtons();
        return;
    }

    btnCloneStride->setEnabled(true);

    // Keep cached filtered notes synchronized with latest ViewModel content
    const QString targetNorm = LogicFormatter::NormalizeAddress(_viewModel->CurrentBaseNote->Address);
    for (auto& fn : _filteredNotes) {
        if (LogicFormatter::NormalizeAddress(fn.Address) == targetNorm) {
            fn.Note = _viewModel->CurrentBaseNote->Note;
            break;
        }
    }

    bool wasUpdating = _isInternalUpdate;
    _isInternalUpdate = true;

    if (!_viewModel->EditState.IsVirtual && _viewModel->EditState.TargetNode == nullptr) {
        QString raw = _viewModel->IsRedirected ? _viewModel->RedirectedContent : _viewModel->CurrentBaseNote->Note;
        QString normalized = raw;
        normalized.replace("\r\n", "\n").replace('\r', '\n');

        txtNoteBody->setPlainText(normalized);
        txtNoteBody->setReadOnly(_viewModel->IsRedirected);
    }

    RefreshVariableTreeDisplay();

    if (cmbVariables->count() > 0 && cmbVariables->currentIndex() == -1)
        cmbVariables->setCurrentIndex(0);

    UpdateStatusDisplay(nullptr);
    UpdateActionButtons();
    _isInternalUpdate = wasUpdating;
}

void CodeNoteEditorWidget::RefreshVariableTreeDisplay() {
    if (_isRefreshingVariables) {
        _variableRefreshPending = true;
        return;
    }
    _isRefreshingVariables = true;
    _variableRefreshPending = false;

    QList<std::shared_ptr<NoteNode>> finalList;
    QString filter = txtSearch->text().trimmed();

    for (const auto& n : _viewModel->CurrentParsedNodes) {
        if (n->IsTemplateInherited)
            continue;

        if (!filter.isEmpty()) {
            bool match = n->Description.contains(filter, Qt::CaseInsensitive) || n->Offset.contains(filter, Qt::CaseInsensitive) ||
                         (n->IndentLevel == -2 && n->Content.contains(filter, Qt::CaseInsensitive));
            if (!match)
                continue;
        }

        if (chkShowLeafsOnly->isChecked()) {
            bool hasRealChildren = false;
            for (const auto& ch : n->Children) {
                if (!ch->IsTemplateInherited) {
                    hasRealChildren = true;
                    break;
                }
            }
            if (n->IndentLevel != -2 && (hasRealChildren || n->IndentLevel == -1))
                continue;
        }

        finalList.append(n);
    }

    QString currentAddr = _viewModel->CurrentBaseNote != nullptr
                              ? LogicFormatter::NormalizeAddress(_viewModel->CurrentBaseNote->Address)
                              : QString();

    int prevIdx = cmbVariables->currentIndex();
    QString prevKey = (!currentAddr.isEmpty() && currentAddr == _lastVariableTreeAddress && prevIdx >= 0 && prevIdx < cmbVariables->count())
                          ? cmbVariables->itemData(prevIdx, Qt::UserRole + 1).toString()
                          : QString();
    _lastVariableTreeAddress = currentAddr;

    {
        QSignalBlocker blocker(cmbVariables);
        cmbVariables->clear();
        int newSelectIdx = -1;

        for (int i = 0; i < finalList.size(); i++) {
            const auto& node = finalList[i];
            QString text = node->ToString();
            if (!chkShowLeafsOnly->isChecked() && node->IndentLevel > 0) {
                QString pad(node->IndentLevel * 2, ' ');
                text = pad + text;
            }
            quintptr nodePtr = reinterpret_cast<quintptr>(node.get());
            QString nodeKey = QString("%1|%2|%3").arg(node->IndentLevel).arg(node->Offset, node->Description);
            cmbVariables->addItem(text, QVariant::fromValue(nodePtr));
            cmbVariables->setItemData(cmbVariables->count() - 1, nodeKey, Qt::UserRole + 1);
            if (!prevKey.isEmpty() && nodeKey == prevKey)
                newSelectIdx = i;
        }

        if (newSelectIdx != -1)
            cmbVariables->setCurrentIndex(newSelectIdx);
    }

    cmbVariables->setEnabled(finalList.size() > 1 && !_viewModel->EditState.IsVirtual);

    _isRefreshingVariables = false;

    if (_variableRefreshPending) {
        _variableRefreshPending = false;
        RefreshVariableTreeDisplay();
    }
}

void CodeNoteEditorWidget::onVmEditStateChanged() {
    const auto& state = _viewModel->EditState;

    bool wasUpdating = _isInternalUpdate;
    _isInternalUpdate = true;

    if (state.IsVirtual) {
        _hasUnsavedChanges = true;
        cmbVariables->setEnabled(false);
        txtNoteBody->setPlainText(state.DisplayText);
        txtNoteBody->setReadOnly(false);
    } else if (state.TargetNode != nullptr) {
        cmbVariables->setEnabled(true);
        txtNoteBody->setPlainText(state.DisplayText);
        txtNoteBody->setReadOnly(_viewModel->IsRedirected);
    } else {
        cmbVariables->setEnabled(true);
        txtNoteBody->setReadOnly(_viewModel->IsRedirected);
    }

    btnFixHierarchy->setEnabled(!txtNoteBody->isReadOnly());

    UpdateStatusDisplay(nullptr);
    UpdateActionButtons();
    _isInternalUpdate = wasUpdating;
}

void CodeNoteEditorWidget::onVmSelectionRequested(std::shared_ptr<NoteNode> node) {
    bool wasUpdating = _isInternalUpdate;
    _isInternalUpdate = true;

    int idx = -1;
    quintptr targetPtr = reinterpret_cast<quintptr>(node.get());
    for (int i = 0; i < cmbVariables->count(); i++) {
        if (cmbVariables->itemData(i, Qt::UserRole).value<quintptr>() == targetPtr) {
            idx = i;
            break;
        }
    }

    if (idx != -1) {
        cmbVariables->setCurrentIndex(idx);
    } else {
        cmbVariables->setCurrentIndex(-1);
    }
    _isInternalUpdate = wasUpdating;
}

void CodeNoteEditorWidget::onNoteIndexChanged(int index) {
    if (_isInternalUpdate || index < 0 || index >= _filteredNotes.size())
        return;
    _viewModel->SelectBaseNote(_filteredNotes[index]);
    _hasUnsavedChanges = false;
    UpdateActionButtons();
}

void CodeNoteEditorWidget::onVariableIndexChanged(int index) {
    if (_isInternalUpdate || index < 0 || index >= cmbVariables->count())
        return;

    quintptr targetPtr = cmbVariables->itemData(index, Qt::UserRole).value<quintptr>();
    for (const auto& node : _viewModel->CurrentParsedNodes) {
        if (reinterpret_cast<quintptr>(node.get()) == targetPtr) {
            _viewModel->RequestNodeSelection(node);
            break;
        }
    }
}

void CodeNoteEditorWidget::onNoteBodyTextChanged() {
    if (_isInternalUpdate)
        return;

    _viewModel->UpdateBufferedText(txtNoteBody->toPlainText());
    _hasUnsavedChanges = true;
    UpdateStatusDisplay(nullptr);
    UpdateActionButtons();
    emit NoteDirtyStateChanged();
}

bool CodeNoteEditorWidget::CanRevertCurrentNote() const {
    if (_viewModel->CurrentBaseNote == nullptr)
        return false;

    const QString normAddr = LogicFormatter::NormalizeAddress(_viewModel->CurrentBaseNote->Address);
    if (!_serverNotesMap.contains(normAddr))
        return false;

    const QString serverText = NormalizeLineEndings(_serverNotesMap.value(normAddr));
    const bool isEditingFullRoot =
        !_viewModel->IsRedirected && !_viewModel->EditState.IsVirtual &&
        (_viewModel->EditState.TargetNode == nullptr || _viewModel->EditState.TargetNode->IndentLevel == -2);

    if (isEditingFullRoot) {
        return NormalizeLineEndings(txtNoteBody->toPlainText()) != serverText;
    }

    if (_hasUnsavedChanges || _viewModel->EditState.IsDirty)
        return true;

    return NormalizeLineEndings(_viewModel->CurrentBaseNote->Note) != serverText;
}

bool CodeNoteEditorWidget::CanResetCurrentNote() const {
    if (_viewModel->CurrentBaseNote == nullptr)
        return false;

    const QString normAddr = LogicFormatter::NormalizeAddress(_viewModel->CurrentBaseNote->Address);
    const bool hasSaved = _savedNotesMap.contains(normAddr);
    const bool hasServer = _serverNotesMap.contains(normAddr);
    if (!hasSaved && !hasServer)
        return false;

    const QString baselineText = NormalizeLineEndings(hasSaved ? _savedNotesMap.value(normAddr) : _serverNotesMap.value(normAddr));
    const bool isEditingFullRoot =
        !_viewModel->IsRedirected && !_viewModel->EditState.IsVirtual &&
        (_viewModel->EditState.TargetNode == nullptr || _viewModel->EditState.TargetNode->IndentLevel == -2);

    if (isEditingFullRoot) {
        return NormalizeLineEndings(txtNoteBody->toPlainText()) != baselineText;
    }

    if (_hasUnsavedChanges || _viewModel->EditState.IsDirty)
        return true;

    return NormalizeLineEndings(_viewModel->CurrentBaseNote->Note) != baselineText;
}

void CodeNoteEditorWidget::UpdateActionButtons() {
    if (btnRevert != nullptr)
        btnRevert->setEnabled(CanRevertCurrentNote());
    if (btnReset != nullptr)
        btnReset->setEnabled(CanResetCurrentNote());
}

void CodeNoteEditorWidget::UpdateStatusDisplay(const QString* overrideMsg) {
    if (overrideMsg != nullptr) {
        lblOffsetChain->setText(*overrideMsg);
        return;
    }

    const auto& state = _viewModel->EditState;
    QString status = "";

    if (state.IsVirtual) {
        status = QString("%1 (New Note)").arg(state.Description);
    } else if (state.TargetNode != nullptr) {
        status = state.Description;
    } else {
        if (_viewModel->IsRedirected)
            status = QString("[Virtual: Linked to %1]").arg(_viewModel->RedirectTargetAddress);
        else
            status = "Ready";
    }

    if (_hasUnsavedChanges || state.IsDirty) {
        if (!status.contains("(Modified"))
            status += " (Modified)";
    }

    lblOffsetChain->setText(status);
}

void CodeNoteEditorWidget::HighlightAddress(const QString& address) {
    _viewModel->HighlightAddress(address);

    if (_viewModel->CurrentBaseNote != nullptr) {
        bool wasUpdating = _isInternalUpdate;
        _isInternalUpdate = true;

        const QString targetNorm = LogicFormatter::NormalizeAddress(_viewModel->CurrentBaseNote->Address);
        bool found = false;
        for (int i = 0; i < _filteredNotes.size(); i++) {
            if (LogicFormatter::NormalizeAddress(_filteredNotes[i].Address) == targetNorm) {
                cmbNotes->setCurrentIndex(i);
                found = true;
                break;
            }
        }
        if (!found) {
            txtSearch->setText("");
            ApplyFilter(false);
            for (int i = 0; i < _filteredNotes.size(); i++) {
                if (LogicFormatter::NormalizeAddress(_filteredNotes[i].Address) == targetNorm) {
                    cmbNotes->setCurrentIndex(i);
                    break;
                }
            }
        }
        _isInternalUpdate = wasUpdating;
    }
    UpdateActionButtons();
}

void CodeNoteEditorWidget::HighlightVariable(const QList<int64_t>& offsets) {
    _viewModel->HighlightVariable(offsets);
    if (_viewModel->CurrentBaseNote != nullptr) {
        bool wasUpdating = _isInternalUpdate;
        _isInternalUpdate = true;

        const QString targetNorm = LogicFormatter::NormalizeAddress(_viewModel->CurrentBaseNote->Address);
        bool found = false;
        for (int i = 0; i < _filteredNotes.size(); i++) {
            if (LogicFormatter::NormalizeAddress(_filteredNotes[i].Address) == targetNorm) {
                cmbNotes->setCurrentIndex(i);
                found = true;
                break;
            }
        }
        if (!found) {
            txtSearch->setText("");
            ApplyFilter(false);
            for (int i = 0; i < _filteredNotes.size(); i++) {
                if (LogicFormatter::NormalizeAddress(_filteredNotes[i].Address) == targetNorm) {
                    cmbNotes->setCurrentIndex(i);
                    break;
                }
            }
        }
        _isInternalUpdate = wasUpdating;
    }
    UpdateActionButtons();
}

std::tuple<QString, QString, QString> CodeNoteEditorWidget::GetSelectedLogic() {
    std::shared_ptr<NoteNode> selectedNode = nullptr;
    int idx = cmbVariables->currentIndex();
    if (idx >= 0 && idx < cmbVariables->count()) {
        quintptr targetPtr = cmbVariables->itemData(idx, Qt::UserRole).value<quintptr>();
        for (const auto& n : _viewModel->CurrentParsedNodes) {
            if (reinterpret_cast<quintptr>(n.get()) == targetPtr) {
                selectedNode = n;
                break;
            }
        }
    }
    return _viewModel->GetSelectedLogic(selectedNode);
}

void CodeNoteEditorWidget::onCutClicked() {
    if (!txtNoteBody->isReadOnly()) {
        _viewModel->PushUndoState();
        txtNoteBody->cut();
    }
}

void CodeNoteEditorWidget::onCopyClicked() {
    if (!txtNoteBody->textCursor().hasSelection())
        return;
    txtNoteBody->copy();
    lblOffsetChain->setText("Copied selection to clipboard.");
}

void CodeNoteEditorWidget::onPasteClicked() {
    if (!txtNoteBody->isReadOnly()) {
        _viewModel->PushUndoState();
        txtNoteBody->paste();
    }
}

void CodeNoteEditorWidget::onDeleteNodeClicked() {
    if (txtNoteBody->isReadOnly())
        return;

    if (_viewModel->EditState.IsVirtual) {
        QMessageBox::information(this, "Invalid Target", "Cannot use Intelligent Delete in Virtual Mode. Please select a real node.");
        return;
    }

    int idx = cmbVariables->currentIndex();
    if (idx >= 0 && idx < cmbVariables->count()) {
        quintptr targetPtr = cmbVariables->itemData(idx, Qt::UserRole).value<quintptr>();
        std::shared_ptr<NoteNode> node = nullptr;
        for (const auto& n : _viewModel->CurrentParsedNodes) {
            if (reinterpret_cast<quintptr>(n.get()) == targetPtr) {
                node = n;
                break;
            }
        }

        if (node != nullptr) {
            if (node->IndentLevel == -2) {
                QMessageBox::information(this, "Invalid Target",
                                         "Cannot intelligently delete the Root note. Use manual text deletion if you want to clear it.");
                return;
            }

            _viewModel->IntelligentDelete(node);
            _hasUnsavedChanges = true;
            QString msg = "Node and orphaned parents deleted.";
            UpdateStatusDisplay(&msg);
            UpdateActionButtons();
            return;
        }
    }

    QMessageBox::information(this, "No Node Selected", "Please select a specific node in the dropdown tree to use Intelligent Delete.");
}

} // namespace RALE
