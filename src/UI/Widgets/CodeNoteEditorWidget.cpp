#include "CodeNoteEditorWidget.h"
#include "../../Parsers/PointerTreeParser.h"
#include "../../Common/AppSettings.h"
#include "../../Common/CodeNoteMultiplier.h"
#include "../../Logic/CodeNoteLogic.h"
#include "../Dialogs/NoteMultiplierDialog.h"
#include "../Styles/ThemeManager.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QMessageBox>
#include <QClipboard>
#include <QGuiApplication>
#include <QKeyEvent>
#include <QRegularExpression>

namespace RALogicEditor {

CodeNoteEditorWidget::CodeNoteEditorWidget(QWidget *parent)
    : QWidget(parent)
{
    static PointerTreeParser staticParser;
    _viewModel = std::make_unique<CodeNoteViewModel>(staticParser, this);

    InitializeUI();

    connect(_viewModel.get(), &CodeNoteViewModel::NotesLoaded, this, [this]() { ApplyFilter(false); });
    connect(_viewModel.get(), &CodeNoteViewModel::NoteContentChanged, this, &CodeNoteEditorWidget::onVmNoteContentChanged);
    connect(_viewModel.get(), &CodeNoteViewModel::EditStateChanged, this, &CodeNoteEditorWidget::onVmEditStateChanged);
    connect(_viewModel.get(), &CodeNoteViewModel::SelectionRequested, this, &CodeNoteEditorWidget::onVmSelectionRequested);
    connect(_viewModel.get(), &CodeNoteViewModel::NoteContentChanged, this, &CodeNoteEditorWidget::NoteUpdated);

    connect(chkShowLeafsOnly, &QCheckBox::toggled, this, &CodeNoteEditorWidget::RefreshVariableTreeDisplay);

    cmbIndentStyle->addItems({
        "Unicode Tree (├─)",
        "Auto Detect (. or +)",
        "Dots (.)",
        "Plusses (+)"
    });

    QString savedStyle = AppSettings::Current.NoteIndentStyle;
    int idx = cmbIndentStyle->findText(savedStyle);
    if (idx != -1) cmbIndentStyle->setCurrentIndex(idx);
    else cmbIndentStyle->setCurrentIndex(0);

    connect(cmbIndentStyle, QOverload<int>::of(&QComboBox::currentIndexChanged), this, [this]() {
        AppSettings::Current.NoteIndentStyle = cmbIndentStyle->currentText();
        AppSettings::Save();
    });
}

void CodeNoteEditorWidget::InitializeUI()
{
    auto *mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(0, 0, 0, 0);
    mainLayout->setSpacing(0);

    auto *panelTop = new QWidget(this);
    auto *topLayout = new QVBoxLayout(panelTop);
    topLayout->setContentsMargins(4, 4, 4, 2);
    topLayout->setSpacing(4);

    // Row 1: Search
    auto *rowSearch = new QHBoxLayout();
    rowSearch->setContentsMargins(0, 0, 0, 0);
    rowSearch->setSpacing(4);
    auto *lblSearch = new QLabel("Search: ", panelTop);
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
    auto *rowAddress = new QHBoxLayout();
    rowAddress->setContentsMargins(0, 0, 0, 0);
    rowAddress->setSpacing(4);
    auto *lblAddress = new QLabel("Address: ", panelTop);
    lblAddress->setFixedWidth(52);
    rowAddress->addWidget(lblAddress);
    cmbNotes = new QComboBox(panelTop);
    ThemeManager::SetupComboBox(cmbNotes, 14);
    connect(cmbNotes, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &CodeNoteEditorWidget::onNoteIndexChanged);
    rowAddress->addWidget(cmbNotes, 1);
    topLayout->addLayout(rowAddress);

    // Row 3: Variable
    auto *rowVariable = new QHBoxLayout();
    rowVariable->setContentsMargins(0, 0, 0, 0);
    rowVariable->setSpacing(4);
    auto *lblVariable = new QLabel("Variable: ", panelTop);
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

    cmsNoteContext = new QMenu(this);
    auto *actCut = cmsNoteContext->addAction("Cut", this, &CodeNoteEditorWidget::onCutClicked);
    actCut->setShortcut(QKeySequence::Cut);
    auto *actCopy = cmsNoteContext->addAction("Copy", this, &CodeNoteEditorWidget::onCopyClicked);
    actCopy->setShortcut(QKeySequence::Copy);
    auto *actPaste = cmsNoteContext->addAction("Paste", this, &CodeNoteEditorWidget::onPasteClicked);
    actPaste->setShortcut(QKeySequence::Paste);
    cmsNoteContext->addSeparator();
    auto *actDelNode = cmsNoteContext->addAction("Intelligent Delete Node (Selected in Tree)", this, &CodeNoteEditorWidget::onDeleteNodeClicked);
    cmsNoteContext->addSeparator();
    cmsNoteContext->addAction("Multiply Selected Text (Inside Note)...", this, &CodeNoteEditorWidget::onMultiplySelectedTextClicked);

    connect(cmsNoteContext, &QMenu::aboutToShow, this, [this, actDelNode]() {
        actDelNode->setEnabled(!_viewModel->EditState.IsVirtual);
    });

    txtNoteBody->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(txtNoteBody, &QWidget::customContextMenuRequested, this, [this](const QPoint &pos) {
        cmsNoteContext->exec(txtNoteBody->mapToGlobal(pos));
    });

    mainLayout->addWidget(txtNoteBody, 1);

    // Panel Bottom (Button toolbar: fixed height 34px with comfortable margins to avoid border clipping)
    auto *panelBottom = new QWidget(this);
    panelBottom->setFixedHeight(34);
    auto *bottomLayout = new QHBoxLayout(panelBottom);
    bottomLayout->setContentsMargins(4, 4, 4, 4);
    bottomLayout->setSpacing(4);

    btnSave = new QPushButton("Save", panelBottom);
    btnSave->setFixedSize(48, 26);
    connect(btnSave, &QPushButton::clicked, this, &CodeNoteEditorWidget::onBtnSaveClicked);
    bottomLayout->addWidget(btnSave);

    btnFixHierarchy = new QPushButton("Fix Hierarchy", panelBottom);
    btnFixHierarchy->setFixedSize(82, 26);
    connect(btnFixHierarchy, &QPushButton::clicked, this, &CodeNoteEditorWidget::onBtnFixHierarchyClicked);
    bottomLayout->addWidget(btnFixHierarchy);

    chkIndentValues = new QCheckBox("Indent", panelBottom);
    chkIndentValues->setChecked(true);
    bottomLayout->addWidget(chkIndentValues);

    // Flexible width dropdown prevents squishing or forcing outer layout overflow
    cmbIndentStyle = new QComboBox(panelBottom);
    cmbIndentStyle->setMinimumWidth(125);
    ThemeManager::SetupComboBox(cmbIndentStyle, 14);
    cmbIndentStyle->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);
    bottomLayout->addWidget(cmbIndentStyle);

    btnReset = new QPushButton("Reset", panelBottom);
    btnReset->setFixedSize(54, 26);
    connect(btnReset, &QPushButton::clicked, this, &CodeNoteEditorWidget::onBtnResetClicked);
    bottomLayout->addWidget(btnReset);

    mainLayout->addWidget(panelBottom);

    // Status Panel: Fixed 22px height container matching TriggerEditor's bottom bar with proper separation
    auto *panelStatus = new QWidget(this);
    panelStatus->setFixedHeight(22);
    auto *statusLayout = new QHBoxLayout(panelStatus);
    statusLayout->setContentsMargins(6, 2, 6, 2);
    statusLayout->setSpacing(0);

    lblOffsetChain = new QLabel("Ready", panelStatus);
    lblOffsetChain->setStyleSheet("color: #707070;");
    statusLayout->addWidget(lblOffsetChain);
    statusLayout->addStretch();

    mainLayout->addWidget(panelStatus);
}

bool CodeNoteEditorWidget::eventFilter(QObject *watched, QEvent *event)
{
    if (watched == txtNoteBody)
    {
        if (event->type() == QEvent::FocusIn)
        {
            if (!txtNoteBody->isReadOnly())
            {
                _viewModel->PushUndoState();
            }
        }
        else if (event->type() == QEvent::KeyPress)
        {
            auto *ke = static_cast<QKeyEvent *>(event);
            if (ke->modifiers() == Qt::ControlModifier && ke->key() == Qt::Key_Z)
            {
                Undo();
                return true;
            }
            else if ((ke->modifiers() == Qt::ControlModifier && ke->key() == Qt::Key_Y) ||
                     (ke->modifiers() == (Qt::ControlModifier | Qt::ShiftModifier) && ke->key() == Qt::Key_Z))
            {
                Redo();
                return true;
            }
        }
    }
    return QWidget::eventFilter(watched, event);
}

void CodeNoteEditorWidget::Undo()
{
    if (_viewModel->CanUndo())
    {
        _viewModel->Undo();
        _hasUnsavedChanges = true;
        QString msg = "Undone.";
        UpdateStatusDisplay(&msg);
    }
}

void CodeNoteEditorWidget::Redo()
{
    if (_viewModel->CanRedo())
    {
        _viewModel->Redo();
        _hasUnsavedChanges = true;
        QString msg = "Redone.";
        UpdateStatusDisplay(&msg);
    }
}

bool CodeNoteEditorWidget::ShowLeafsOnly() const { return chkShowLeafsOnly->isChecked(); }
void CodeNoteEditorWidget::SetShowLeafsOnly(bool value) { chkShowLeafsOnly->setChecked(value); }

bool CodeNoteEditorWidget::IndentValues() const { return chkIndentValues->isChecked(); }
void CodeNoteEditorWidget::SetIndentValues(bool value) { chkIndentValues->setChecked(value); }

QString CodeNoteEditorWidget::NoteIndentStyle() const { return cmbIndentStyle->currentText(); }
void CodeNoteEditorWidget::SetNoteIndentStyle(const QString &style)
{
    int idx = cmbIndentStyle->findText(style);
    if (idx != -1) cmbIndentStyle->setCurrentIndex(idx);
}

void CodeNoteEditorWidget::onBtnReloadClicked()
{
    emit ReloadRequested();
}

void CodeNoteEditorWidget::onBtnSaveClicked()
{
    CommitChanges();
    emit SaveRequested();
    _hasUnsavedChanges = false;
    QString msg = "Notes saved.";
    UpdateStatusDisplay(&msg);
}

void CodeNoteEditorWidget::onBtnResetClicked()
{
    if (cmbNotes->currentIndex() >= 0 && cmbNotes->currentIndex() < _filteredNotes.size())
    {
        const auto &note = _filteredNotes[cmbNotes->currentIndex()];
        auto result = QMessageBox::warning(
            this,
            "Reset Note",
            QString("Are you sure you want to reset the note for %1 to its original state?\n\nThis will delete your local modifications and reload the file.")
                .arg(note.Address),
            QMessageBox::Yes | QMessageBox::No,
            QMessageBox::No);

        if (result == QMessageBox::Yes)
        {
            emit NoteResetRequested(note.Address);
        }
    }
}

void CodeNoteEditorWidget::onBtnFixHierarchyClicked()
{
    if (txtNoteBody->isReadOnly()) return;

    QString currentText = txtNoteBody->toPlainText();
    if (currentText.trimmed().isEmpty()) return;

    _viewModel->PushUndoState();

    QString fixedText = CodeNoteLogic::FixHierarchy(
        currentText,
        cmbIndentStyle->currentText(),
        chkIndentValues->isChecked()
    );

    if (txtNoteBody->toPlainText() != fixedText)
    {
        txtNoteBody->setPlainText(fixedText);
        _viewModel->UpdateNoteContent(fixedText);
        _hasUnsavedChanges = true;
        QString msg = "Hierarchy fixed.";
        UpdateStatusDisplay(&msg);
    }
}

void CodeNoteEditorWidget::CommitChanges()
{
    _viewModel->CommitEditState();
    // Synchronize local filtered notes to reflect committed content immediately
    if (_viewModel->CurrentBaseNote != nullptr)
    {
        for (auto &fn : _filteredNotes)
        {
            if (fn.Address.compare(_viewModel->CurrentBaseNote->Address, Qt::CaseInsensitive) == 0)
            {
                fn.Note = _viewModel->CurrentBaseNote->Note;
                break;
            }
        }
    }
}

void CodeNoteEditorWidget::SetNotes(const QList<CodeNote> &notes)
{
    _isInternalUpdate = true;
    QString previousAddress = "";
    if (cmbNotes->currentIndex() >= 0 && cmbNotes->currentIndex() < _filteredNotes.size())
        previousAddress = _filteredNotes[cmbNotes->currentIndex()].Address;

    _viewModel->LoadNotes(notes);

    cmbNotes->clear();
    txtNoteBody->clear();
    _viewModel->ClearEditState();

    ApplyFilter(false);

    bool selectionRestored = false;
    if (!previousAddress.isEmpty())
    {
        for (int i = 0; i < _filteredNotes.size(); i++)
        {
            if (_filteredNotes[i].Address == previousAddress)
            {
                cmbNotes->setCurrentIndex(i);
                _viewModel->SelectBaseNote(_filteredNotes[i]);
                selectionRestored = true;
                break;
            }
        }
    }

    if (!selectionRestored && !_filteredNotes.isEmpty())
    {
        cmbNotes->setCurrentIndex(0);
        _viewModel->SelectBaseNote(_filteredNotes[0]);
    }
    _isInternalUpdate = false;
}

void CodeNoteEditorWidget::ApplyFilter(bool autoSelectFirst)
{
    QString filter = txtSearch->text().trimmed();
    if (filter.isEmpty())
    {
        _filteredNotes = _viewModel->AllNotes;
    }
    else
    {
        _filteredNotes.clear();
        for (const auto &n : _viewModel->AllNotes)
        {
            if (n.Address.contains(filter, Qt::CaseInsensitive) || n.Note.contains(filter, Qt::CaseInsensitive))
                _filteredNotes.append(n);
        }
    }

    cmbNotes->clear();
    for (const auto &n : _filteredNotes)
    {
        // Split on any newline flavour so a note beginning with CRLF still shows its
        // first real line (C# uses RemoveEmptyEntries on {'\r','\n'}).
        QStringList lines = n.Note.split(QRegularExpression("[\r\n]+"), Qt::SkipEmptyParts);
        QString firstLine = lines.isEmpty() ? QString() : lines.first().trimmed();
        if (firstLine.length() > 50) firstLine = firstLine.left(47) + "...";
        cmbNotes->addItem(QString("%1 | %2").arg(n.Address, firstLine));
    }

    if (autoSelectFirst && !_filteredNotes.isEmpty())
    {
        cmbNotes->setCurrentIndex(0);
        _viewModel->SelectBaseNote(_filteredNotes[0]);
    }
    else if (_filteredNotes.isEmpty())
    {
        cmbVariables->clear();
        QString msg = "No matches found.";
        UpdateStatusDisplay(&msg);
    }
}

void CodeNoteEditorWidget::onSearchTextChanged(const QString & /*text*/)
{
    if (!_isInternalUpdate) ApplyFilter(true);
}

void CodeNoteEditorWidget::onVmNoteContentChanged()
{
    if (_viewModel->CurrentBaseNote == nullptr) return;

    // Keep cached filtered notes synchronized with latest ViewModel content
    for (auto &fn : _filteredNotes)
    {
        if (fn.Address.compare(_viewModel->CurrentBaseNote->Address, Qt::CaseInsensitive) == 0)
        {
            fn.Note = _viewModel->CurrentBaseNote->Note;
            break;
        }
    }

    bool wasUpdating = _isInternalUpdate;
    _isInternalUpdate = true;

    if (!_viewModel->EditState.IsVirtual && _viewModel->EditState.TargetNode == nullptr)
    {
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
    _isInternalUpdate = wasUpdating;
}

void CodeNoteEditorWidget::RefreshVariableTreeDisplay()
{
    // addItem() selects the first item, which can commit a pending edit and emit
    // NoteContentChanged synchronously. Without this guard that re-enters this
    // function mid-loop and leaves duplicate/stale entries in the dropdown.
    if (_isRefreshingVariables)
    {
        // A nested refresh (usually caused by committing a pending edit from
        // addItem's automatic selection) must rebuild again with the new tree
        // once the outer rebuild is done.
        _variableRefreshPending = true;
        return;
    }
    _isRefreshingVariables = true;
    _variableRefreshPending = false;

    QList<std::shared_ptr<NoteNode>> finalList;
    QString filter = txtSearch->text().trimmed();

    for (const auto &n : _viewModel->CurrentParsedNodes)
    {
        if (!filter.isEmpty())
        {
            bool match = n->Description.contains(filter, Qt::CaseInsensitive) ||
                         n->Offset.contains(filter, Qt::CaseInsensitive) ||
                         (n->IndentLevel == -2 && n->Content.contains(filter, Qt::CaseInsensitive));
            if (!match) continue;
        }

        if (chkShowLeafsOnly->isChecked())
        {
            if (n->IndentLevel != -2 && (n->Children.size() > 0 || n->IndentLevel == -1))
                continue;
        }

        finalList.append(n);
    }

    int prevIdx = cmbVariables->currentIndex();
    quintptr prevPtr = (prevIdx >= 0 && prevIdx < cmbVariables->count())
        ? cmbVariables->itemData(prevIdx, Qt::UserRole).value<quintptr>() : 0;

    cmbVariables->clear();
    int newSelectIdx = -1;

    for (int i = 0; i < finalList.size(); i++)
    {
        const auto &node = finalList[i];
        QString text = node->ToString();
        if (!chkShowLeafsOnly->isChecked() && node->IndentLevel > 0)
        {
            QString pad(node->IndentLevel * 2, ' ');
            text = pad + text;
        }
        quintptr nodePtr = reinterpret_cast<quintptr>(node.get());
        cmbVariables->addItem(text, QVariant::fromValue(nodePtr));
        if (prevPtr != 0 && nodePtr == prevPtr)
            newSelectIdx = i;
    }

    if (newSelectIdx != -1)
        cmbVariables->setCurrentIndex(newSelectIdx);

    cmbVariables->setEnabled(finalList.size() > 1 && !_viewModel->EditState.IsVirtual);

    _isRefreshingVariables = false;

    if (_variableRefreshPending)
    {
        _variableRefreshPending = false;
        RefreshVariableTreeDisplay();
    }
}

void CodeNoteEditorWidget::onVmEditStateChanged()
{
    const auto &state = _viewModel->EditState;

    bool wasUpdating = _isInternalUpdate;
    _isInternalUpdate = true;

    if (state.IsVirtual)
    {
        _hasUnsavedChanges = true;
        cmbVariables->setEnabled(false);
        txtNoteBody->setPlainText(state.DisplayText);
        txtNoteBody->setReadOnly(false);
    }
    else if (state.TargetNode != nullptr)
    {
        cmbVariables->setEnabled(true);
        txtNoteBody->setPlainText(state.DisplayText);
        txtNoteBody->setReadOnly(_viewModel->IsRedirected);
    }
    else
    {
        cmbVariables->setEnabled(true);
        txtNoteBody->setReadOnly(_viewModel->IsRedirected);
    }

    btnFixHierarchy->setEnabled(!txtNoteBody->isReadOnly());

    UpdateStatusDisplay(nullptr);
    _isInternalUpdate = wasUpdating;
}

void CodeNoteEditorWidget::onVmSelectionRequested(std::shared_ptr<NoteNode> node)
{
    bool wasUpdating = _isInternalUpdate;
    _isInternalUpdate = true;

    int idx = -1;
    quintptr targetPtr = reinterpret_cast<quintptr>(node.get());
    for (int i = 0; i < cmbVariables->count(); i++)
    {
        if (cmbVariables->itemData(i, Qt::UserRole).value<quintptr>() == targetPtr)
        {
            idx = i;
            break;
        }
    }

    if (idx != -1)
    {
        cmbVariables->setCurrentIndex(idx);
    }
    else
    {
        cmbVariables->setCurrentIndex(-1);
    }
    _isInternalUpdate = wasUpdating;
}

void CodeNoteEditorWidget::onNoteIndexChanged(int index)
{
    if (_isInternalUpdate || index < 0 || index >= _filteredNotes.size()) return;
    _viewModel->SelectBaseNote(_filteredNotes[index]);
    _hasUnsavedChanges = false;
}

void CodeNoteEditorWidget::onVariableIndexChanged(int index)
{
    if (_isInternalUpdate || index < 0 || index >= cmbVariables->count()) return;

    quintptr targetPtr = cmbVariables->itemData(index, Qt::UserRole).value<quintptr>();
    for (const auto &node : _viewModel->CurrentParsedNodes)
    {
        if (reinterpret_cast<quintptr>(node.get()) == targetPtr)
        {
            if (node->IndentLevel == -2)
            {
                _viewModel->ClearEditState();
                _viewModel->RequestNodeSelection(node);
            }
            else
            {
                _viewModel->RequestNodeSelection(node);
            }
            break;
        }
    }
}

void CodeNoteEditorWidget::onNoteBodyTextChanged()
{
    if (_isInternalUpdate) return;

    _viewModel->UpdateBufferedText(txtNoteBody->toPlainText());
    _hasUnsavedChanges = true;
    UpdateStatusDisplay(nullptr);
}

void CodeNoteEditorWidget::UpdateStatusDisplay(const QString *overrideMsg)
{
    if (overrideMsg != nullptr)
    {
        lblOffsetChain->setText(*overrideMsg);
        return;
    }

    const auto &state = _viewModel->EditState;
    QString status = "";

    if (state.IsVirtual)
    {
        status = QString("%1 (New Note)").arg(state.Description);
    }
    else if (state.TargetNode != nullptr)
    {
        status = state.Description;
    }
    else
    {
        if (_viewModel->IsRedirected)
            status = QString("[Virtual: Linked to %1]").arg(_viewModel->RedirectTargetAddress);
        else
            status = "Ready";
    }

    if (_hasUnsavedChanges || state.IsDirty)
    {
        if (!status.contains("(Modified"))
            status += " (Modified)";
    }

    lblOffsetChain->setText(status);
}

void CodeNoteEditorWidget::HighlightAddress(const QString &address)
{
    _viewModel->HighlightAddress(address);

    if (_viewModel->CurrentBaseNote != nullptr)
    {
        bool wasUpdating = _isInternalUpdate;
        _isInternalUpdate = true;

        bool found = false;
        for (int i = 0; i < _filteredNotes.size(); i++)
        {
            if (_filteredNotes[i].Address == _viewModel->CurrentBaseNote->Address)
            {
                cmbNotes->setCurrentIndex(i);
                found = true;
                break;
            }
        }
        if (!found)
        {
            txtSearch->setText("");
            ApplyFilter(false);
            for (int i = 0; i < _filteredNotes.size(); i++)
            {
                if (_filteredNotes[i].Address == _viewModel->CurrentBaseNote->Address)
                {
                    cmbNotes->setCurrentIndex(i);
                    break;
                }
            }
        }
        _isInternalUpdate = wasUpdating;
    }
}

void CodeNoteEditorWidget::HighlightVariable(const QList<int64_t> &offsets)
{
    _viewModel->HighlightVariable(offsets);
    if (_viewModel->CurrentBaseNote != nullptr)
    {
        bool wasUpdating = _isInternalUpdate;
        _isInternalUpdate = true;

        bool found = false;
        for (int i = 0; i < _filteredNotes.size(); i++)
        {
            if (_filteredNotes[i].Address == _viewModel->CurrentBaseNote->Address)
            {
                cmbNotes->setCurrentIndex(i);
                found = true;
                break;
            }
        }
        if (!found)
        {
            txtSearch->setText("");
            ApplyFilter(false);
            for (int i = 0; i < _filteredNotes.size(); i++)
            {
                if (_filteredNotes[i].Address == _viewModel->CurrentBaseNote->Address)
                {
                    cmbNotes->setCurrentIndex(i);
                    break;
                }
            }
        }
        _isInternalUpdate = wasUpdating;
    }
}

std::tuple<QString, QString, QString> CodeNoteEditorWidget::GetSelectedLogic()
{
    std::shared_ptr<NoteNode> selectedNode = nullptr;
    int idx = cmbVariables->currentIndex();
    if (idx >= 0 && idx < cmbVariables->count())
    {
        quintptr targetPtr = cmbVariables->itemData(idx, Qt::UserRole).value<quintptr>();
        for (const auto &n : _viewModel->CurrentParsedNodes)
        {
            if (reinterpret_cast<quintptr>(n.get()) == targetPtr) { selectedNode = n; break; }
        }
    }
    return _viewModel->GetSelectedLogic(selectedNode);
}

void CodeNoteEditorWidget::onCutClicked()
{
    if (!txtNoteBody->isReadOnly())
    {
        _viewModel->PushUndoState();
        txtNoteBody->cut();
    }
}

void CodeNoteEditorWidget::onCopyClicked()
{
    if (!txtNoteBody->textCursor().hasSelection()) return;
    txtNoteBody->copy();
    lblOffsetChain->setText("Copied selection to clipboard.");
}

void CodeNoteEditorWidget::onPasteClicked()
{
    if (!txtNoteBody->isReadOnly())
    {
        _viewModel->PushUndoState();
        txtNoteBody->paste();
    }
}

void CodeNoteEditorWidget::onDeleteNodeClicked()
{
    if (txtNoteBody->isReadOnly()) return;

    if (_viewModel->EditState.IsVirtual)
    {
        QMessageBox::information(this, "Invalid Target", "Cannot use Intelligent Delete in Virtual Mode. Please select a real node.");
        return;
    }

    int idx = cmbVariables->currentIndex();
    if (idx >= 0 && idx < cmbVariables->count())
    {
        quintptr targetPtr = cmbVariables->itemData(idx, Qt::UserRole).value<quintptr>();
        std::shared_ptr<NoteNode> node = nullptr;
        for (const auto &n : _viewModel->CurrentParsedNodes)
        {
            if (reinterpret_cast<quintptr>(n.get()) == targetPtr) { node = n; break; }
        }

        if (node != nullptr)
        {
            if (node->IndentLevel == -2)
            {
                QMessageBox::information(this, "Invalid Target", "Cannot intelligently delete the Root note. Use manual text deletion if you want to clear it.");
                return;
            }

            _viewModel->IntelligentDelete(node);
            _hasUnsavedChanges = true;
            QString msg = "Node and orphaned parents deleted.";
            UpdateStatusDisplay(&msg);
            return;
        }
    }

    QMessageBox::information(this, "No Node Selected", "Please select a specific node in the dropdown tree to use Intelligent Delete.");
}

void CodeNoteEditorWidget::onMultiplySelectedTextClicked()
{
    if (txtNoteBody->isReadOnly()) return;

    QString targetText = txtNoteBody->textCursor().selectedText();
    targetText.replace(QChar(0x2029), "\n");

    if (targetText.isEmpty())
    {
        QMessageBox::information(this, "No Selection", "Please highlight the block of text you want to duplicate before clicking this option.");
        return;
    }

    NoteMultiplierDialog form(this);
    if (form.exec() == QDialog::Accepted)
    {
        QString generated = CodeNoteMultiplier::GenerateText(
            targetText,
            form.Copies,
            form.Stride,
            form.Naming,
            form.SearchText,
            form.ReplaceText,
            form.StartNumber,
            form.TargetMode);

        _viewModel->PushUndoState();
        auto cursor = txtNoteBody->textCursor();
        int insertPos = cursor.selectionEnd();
        QString current = txtNoteBody->toPlainText();
        current.insert(insertPos, "\n" + generated);
        txtNoteBody->setPlainText(current);

        // Restore the block selection around the generated text (setPlainText moves
        // the caret to the start of the document).
        auto selectCursor = txtNoteBody->textCursor();
        selectCursor.setPosition(insertPos + 1);
        selectCursor.setPosition(insertPos + 1 + generated.length(), QTextCursor::KeepAnchor);
        txtNoteBody->setTextCursor(selectCursor);

        _viewModel->CommitEditState();

        _hasUnsavedChanges = true;
        QString msg = "Note multiplied.";
        UpdateStatusDisplay(&msg);
    }
}

void CodeNoteEditorWidget::ExecuteBatchMultiply(
    const QList<CodeNote> &selectedNotes,
    const QString &searchKeyword,
    const QString &replacePattern,
    int copies,
    int64_t stride,
    int startNum)
{
    _viewModel->PushUndoState();
    _viewModel->BatchMultiplyNotes(selectedNotes, searchKeyword, replacePattern, copies, stride, startNum);
    _hasUnsavedChanges = true;
    QString msg = QString("Batch duplicated %1 note(s).").arg(selectedNotes.size());
    UpdateStatusDisplay(&msg);
    emit NoteUpdated();
}

} // namespace RALogicEditor
