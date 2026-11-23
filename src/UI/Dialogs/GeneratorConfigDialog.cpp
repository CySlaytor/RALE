#include "GeneratorConfigDialog.h"

#include "../../Common/AppSettings.h"
#include "../../Common/ConditionFormatter.h"
#include "../../Definitions/RaLogicSyntax.h"
#include "../../Parsers/PointerTreeParser.h"
#include "../../Services/CodeNoteTemplateService.h"
#include "../../Utilities/LogicFormatter.h"

#include <QApplication>
#include <QClipboard>
#include <QFormLayout>
#include <QGroupBox>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QLabel>
#include <QMessageBox>
#include <QRegularExpression>
#include <QSignalBlocker>
#include <QVBoxLayout>

#include <algorithm>
#include <cmath>

namespace RALE {

namespace {

bool TryParseHex(const QString& input, int64_t& outVal) {
    QString clean = input.trimmed();
    clean.remove('|');
    clean = clean.trimmed();
    bool isNeg = clean.startsWith('-');
    if (isNeg || clean.startsWith('+'))
        clean = clean.mid(1).trimmed();
    if (clean.startsWith("0x", Qt::CaseInsensitive))
        clean = clean.mid(2).trimmed();
    if (clean.isEmpty())
        return false;

    bool ok = false;
    uint64_t uv = clean.toULongLong(&ok, 16);
    if (!ok)
        return false;
    outVal = isNeg ? -static_cast<int64_t>(uv) : static_cast<int64_t>(uv);
    return true;
}

bool IsMeaningfulNote(const QString& noteText) {
    QString trimmed = noteText.trimmed();
    return !trimmed.isEmpty() && trimmed != "[No code note for this address]";
}

QList<std::shared_ptr<NoteNode>> TopLevelNodesOf(const QList<std::shared_ptr<NoteNode>>& parsedNodes,
                                                 std::shared_ptr<NoteNode>* outRoot = nullptr) {
    QList<std::shared_ptr<NoteNode>> top;
    std::shared_ptr<NoteNode> root = nullptr;
    for (const auto& n : parsedNodes) {
        if (n->IndentLevel == -2) {
            root = n;
            for (const auto& ch : n->Children) {
                if (ch->IndentLevel == -1)
                    top.append(ch->Children);
                else if (ch->IndentLevel != -2)
                    top.append(ch);
            }
            break;
        }
    }
    if (top.isEmpty()) {
        for (const auto& n : parsedNodes) {
            if (n->IndentLevel != -2 && (n->Parent == nullptr || n->Parent->IndentLevel == -1))
                top.append(n);
        }
    }
    if (outRoot != nullptr)
        *outRoot = root;
    return top;
}

QString FormatHexTarget(int64_t val, bool withSign) {
    if (withSign) {
        QString sign = (val < 0) ? "-" : "+";
        QString hex = QString::number(std::abs(val), 16).toLower();
        if (hex.length() < 2)
            hex = hex.rightJustified(2, '0');
        return sign + "0x" + hex;
    }
    return QString("0x%1").arg(static_cast<quint64>(val), 0, 16).toLower();
}

} // namespace

bool GeneratorConfigDialog::_userModifiedStride = false; // Tracks if the user has explicitly set a stride this session

QString GeneratorConfigDialog::GeneratorItemViewModel::ToString() const {
    if (Description.isEmpty())
        return Address;

    // Strip brackets [32-bit] etc for cleaner display, as requested
    static const QRegularExpression bracketRegex(R"(\[.*?\])");
    QString cleanDesc = Description;
    cleanDesc.remove(bracketRegex);
    cleanDesc = cleanDesc.trimmed();

    if (cleanDesc.isEmpty())
        return Address;
    return QString("%1 (%2)").arg(Address, cleanDesc);
}

// Constructor now accepts a hierarchical list of items and pre-selected addresses
GeneratorConfigDialog::GeneratorConfigDialog(const QList<std::shared_ptr<GeneratorItemViewModel>>& rootItems, int defaultStride,
                                             const GeneratorDefinition* existingDef, const QSet<QString>* preSelectedAddresses,
                                             QWidget* parent, const QList<CodeNote>* notes,
                                             const AchievementConditionGroup* sourceGroup)
    : QDialog(parent), _notes(notes), _sourceGroup(sourceGroup) {
    InitializeUI();
    InitializeTreeView(rootItems);

    // 1. Apply Persistence Defaults First
    txtCopies->setText(AppSettings::Current.Generator_Copies);
    txtStride->setText(AppSettings::Current.Generator_Stride);
    chkGenerateAsArray->setChecked(AppSettings::Current.Generator_IsArray);
    chkDuplicateSelectedOnly->setChecked(AppSettings::Current.Generator_DuplicateSelectedOnly);
    chkDuplicateNotes->setChecked(AppSettings::Current.Generator_DuplicateNotes);

    // 2. Pre-fill Stride if auto-detected (Only override if the user hasn't explicitly customized it this session)
    if (defaultStride > 0 && !_userModifiedStride) {
        txtStride->setText("0x" + QString::number(defaultStride, 16).toUpper());
    }

    // 3. Determine Checkbox States
    if (existingDef != nullptr && !existingDef->Rules.isEmpty()) {
        // If we are editing an existing generator, override with its stored definition
        txtCopies->setText(QString::number(existingDef->Copies));
        chkGenerateAsArray->setChecked(existingDef->IsArray);
        chkDuplicateSelectedOnly->setChecked(existingDef->DuplicateSelectedOnly);
        chkDuplicateNotes->setChecked(existingDef->DuplicateNotes);

        int foundStride = existingDef->Rules[0].Stride;
        txtStride->setText("0x" + QString::number(foundStride, 16).toUpper());

        for (const auto& r : existingDef->Rules) {
            if (!r.NoteHeaderTemplate.isEmpty() || !r.NoteCustomNames.isEmpty() || !r.NoteExplicitTitles.isEmpty()) {
                QString key = LogicFormatter::NormalizeAddress(r.SourceAddress);
                NoteOverrides[key] = NoteOverrideConfig{r.NoteHeaderTemplate, r.NoteCustomNames, r.NoteExplicitTitles};
            }
        }

        // Check addresses that are already in the rules
        QSet<QString> rulesSet;
        for (const auto& r : existingDef->Rules)
            rulesSet.insert(r.SourceAddress);

        _isUpdatingCheck = true;
        for (int i = 0; i < tvAddresses->topLevelItemCount(); i++)
            CheckNodesRecursive(tvAddresses->topLevelItem(i), rulesSet);
        _isUpdatingCheck = false;
    } else if (preSelectedAddresses != nullptr && !preSelectedAddresses->isEmpty()) {
        if (existingDef != nullptr) {
            txtCopies->setText(QString::number(existingDef->Copies));
            chkGenerateAsArray->setChecked(existingDef->IsArray);
        }

        // Pre-check nodes based on what the user had selected in the Editor Grid
        _isUpdatingCheck = true;
        for (int i = 0; i < tvAddresses->topLevelItemCount(); i++)
            CheckNodesRecursive(tvAddresses->topLevelItem(i), *preSelectedAddresses);
        _isUpdatingCheck = false;
    } else if (tvAddresses->topLevelItemCount() == 1) {
        if (existingDef != nullptr) {
            txtCopies->setText(QString::number(existingDef->Copies));
            chkGenerateAsArray->setChecked(existingDef->IsArray);
        }

        // Select single root item by default for convenience if nothing else guides us
        _isUpdatingCheck = true;
        tvAddresses->topLevelItem(0)->setCheckState(0, Qt::Checked);
        // Propagate check to children
        SetChildrenChecked(tvAddresses->topLevelItem(0), true);
        _isUpdatingCheck = false;
    }

    chkDuplicateSelectedOnly->setEnabled(chkGenerateAsArray->isChecked());
    UpdatePreviewPanelVisibility(chkDuplicateNotes->isChecked());
    loadPositionSettings();
}

void GeneratorConfigDialog::loadPositionSettings() {
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("GeneratorConfigDialog", p, s)) {
        move(p);
    }
}

void GeneratorConfigDialog::savePositionSettings() { AppSettings::SaveDialogGeometry("GeneratorConfigDialog", pos()); }

void GeneratorConfigDialog::done(int r) {
    savePositionSettings();
    QDialog::done(r);
}

void GeneratorConfigDialog::InitializeUI() {
    setWindowTitle("Create Generator");

    auto* outerLayout = new QHBoxLayout(this);
    outerLayout->setContentsMargins(12, 9, 12, 10);
    outerLayout->setSpacing(12);

    // Left Panel: Address Tree + Copies / Stride + Options
    auto* leftWidget = new QWidget(this);
    leftWidget->setFixedWidth(340);
    auto* mainLayout = new QVBoxLayout(leftWidget);
    mainLayout->setContentsMargins(0, 0, 0, 0);
    mainLayout->setSpacing(5);

    auto* lblInstructions = new QLabel("Select Base Addresses to Expand:", leftWidget);
    QFont f = lblInstructions->font();
    f.setBold(true);
    lblInstructions->setFont(f);
    mainLayout->addWidget(lblInstructions);

    tvAddresses = new QTreeWidget(leftWidget);
    tvAddresses->setObjectName("tvAddresses");
    tvAddresses->setHeaderHidden(true);
    tvAddresses->setAnimated(false);
    tvAddresses->setIndentation(19);
    tvAddresses->setRootIsDecorated(true);
    connect(tvAddresses, &QTreeWidget::itemChanged, this, &GeneratorConfigDialog::onTreeItemChanged);
    mainLayout->addWidget(tvAddresses, 1);

    auto* topBtnRow = new QHBoxLayout();
    topBtnRow->setContentsMargins(0, 0, 0, 0);
    topBtnRow->addStretch();
    btnSelectAll = new QPushButton("Select All", leftWidget);
    btnSelectAll->setFixedSize(75, 23);
    connect(btnSelectAll, &QPushButton::clicked, this, &GeneratorConfigDialog::onSelectAll);
    topBtnRow->addWidget(btnSelectAll);
    mainLayout->addLayout(topBtnRow);

    auto* formLayout = new QFormLayout();
    formLayout->setContentsMargins(0, 2, 0, 0);
    formLayout->setSpacing(5);
    formLayout->setLabelAlignment(Qt::AlignLeft);

    txtCopies = new QLineEdit("1", leftWidget);
    txtCopies->setFixedSize(100, 23);
    connect(txtCopies, &QLineEdit::textChanged, this, &GeneratorConfigDialog::onParametersChanged);
    formLayout->addRow("Number of Copies:", txtCopies);

    txtStride = new QLineEdit("0x0", leftWidget);
    txtStride->setFixedSize(100, 23);
    connect(txtStride, &QLineEdit::textChanged, this, &GeneratorConfigDialog::onParametersChanged);
    formLayout->addRow("Stride (Address):", txtStride);

    auto* lblStrideHint = new QLabel("Hex supported (0x)", leftWidget);
    lblStrideHint->setObjectName("lblStrideHint");
    formLayout->addRow("", lblStrideHint);

    mainLayout->addLayout(formLayout);

    chkGenerateAsArray = new QCheckBox("Generate as Array (Append to this Group, don't create Alts)", leftWidget);
    connect(chkGenerateAsArray, &QCheckBox::toggled, this, &GeneratorConfigDialog::onParametersChanged);
    mainLayout->addWidget(chkGenerateAsArray);

    chkDuplicateSelectedOnly = new QCheckBox("Duplicate only selected address(es)", leftWidget);
    chkDuplicateSelectedOnly->setToolTip(
        "When generating an array, only duplicate conditions containing the checked address(es) instead of the entire group");
    chkDuplicateSelectedOnly->setEnabled(false);
    connect(chkGenerateAsArray, &QCheckBox::toggled, chkDuplicateSelectedOnly, &QWidget::setEnabled);
    auto* subOptionRow = new QHBoxLayout();
    subOptionRow->setContentsMargins(18, 0, 0, 0);
    subOptionRow->addWidget(chkDuplicateSelectedOnly);
    mainLayout->addLayout(subOptionRow);

    chkDuplicateNotes = new QCheckBox("Duplicate associated Code Notes", leftWidget);
    chkDuplicateNotes->setToolTip(
        "Automatically duplicates code notes (including AddAddress pointer chain entries) for generated addresses, skipping any that "
        "already exist");
    connect(chkDuplicateNotes, &QCheckBox::toggled, this, &GeneratorConfigDialog::onDuplicateNotesToggled);
    mainLayout->addWidget(chkDuplicateNotes);

    auto* btnRow = new QHBoxLayout();
    btnRow->setContentsMargins(0, 6, 0, 0);
    btnRow->addStretch();
    btnOK = new QPushButton("OK", leftWidget);
    btnOK->setFixedSize(75, 23);
    connect(btnOK, &QPushButton::clicked, this, &GeneratorConfigDialog::onBtnOK);
    btnCancel = new QPushButton("Cancel", leftWidget);
    btnCancel->setFixedSize(75, 23);
    connect(btnCancel, &QPushButton::clicked, this, &GeneratorConfigDialog::onBtnCancel);
    btnRow->addWidget(btnOK);
    btnRow->addWidget(btnCancel);
    mainLayout->addLayout(btnRow);

    outerLayout->addWidget(leftWidget);

    // Right Panel: Clone-Stride-Style Note Template & Live Preview
    _notePreviewPanel = new QWidget(this);
    auto* rightLayout = new QVBoxLayout(_notePreviewPanel);
    rightLayout->setContentsMargins(0, 0, 0, 0);
    rightLayout->setSpacing(6);

    auto* tmplGroup = new QGroupBox("Code Note Template && Tokens", _notePreviewPanel);
    auto* tmplGrid = new QFormLayout(tmplGroup);
    tmplGrid->setContentsMargins(8, 12, 8, 8);
    tmplGrid->setSpacing(6);

    _cmbNoteSource = new QComboBox(tmplGroup);
    connect(_cmbNoteSource, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &GeneratorConfigDialog::onNoteSourceChanged);
    tmplGrid->addRow("Source Note:", _cmbNoteSource);

    _txtNoteTemplate = new QLineEdit(tmplGroup);
    connect(_txtNoteTemplate, &QLineEdit::textChanged, this, &GeneratorConfigDialog::onNoteTemplateChanged);
    tmplGrid->addRow("Header Template:", _txtNoteTemplate);

    auto* lblTokensHint = new QLabel("Tokens: <b>{INDEX}</b>, <b>{INDEX:02}</b>, <b>{OFFSET}</b>, <b>{NAME}</b>", tmplGroup);
    lblTokensHint->setObjectName("subTextDescLabel");
    tmplGrid->addRow("", lblTokensHint);

    rightLayout->addWidget(tmplGroup);

    auto* previewHeader = new QHBoxLayout();
    auto* lblPreviewTitle = new QLabel("<b>Generated Code Notes (Editable Preview):</b>", _notePreviewPanel);
    previewHeader->addWidget(lblPreviewTitle);
    previewHeader->addStretch();

    _btnPasteNames = new QPushButton("Paste Names from Clipboard", _notePreviewPanel);
    _btnPasteNames->setFixedHeight(24);
    _btnPasteNames->setAutoDefault(false);
    _btnPasteNames->setDefault(false);
    _btnPasteNames->setToolTip("Paste multi-line text from clipboard to populate {NAME} values for generated rows");
    connect(_btnPasteNames, &QPushButton::clicked, this, &GeneratorConfigDialog::onPasteNamesClicked);
    previewHeader->addWidget(_btnPasteNames);
    rightLayout->addLayout(previewHeader);

    _tblNotePreview = new QTableWidget(_notePreviewPanel);
    _tblNotePreview->setColumnCount(5);
    _tblNotePreview->setHorizontalHeaderLabels({"Copy", "Target", "Status", "Name ({NAME})", "Generated Note Title (Editable)"});
    _tblNotePreview->setAlternatingRowColors(true);
    _tblNotePreview->verticalHeader()->setVisible(false);
    _tblNotePreview->verticalHeader()->setDefaultSectionSize(22);
    _tblNotePreview->setSelectionBehavior(QAbstractItemView::SelectRows);
    _tblNotePreview->setEditTriggers(QAbstractItemView::DoubleClicked | QAbstractItemView::SelectedClicked |
                                     QAbstractItemView::EditKeyPressed);

    QFont monoFont("Consolas", 9);
    monoFont.setStyleHint(QFont::Monospace);
    _tblNotePreview->setFont(monoFont);

    _tblNotePreview->horizontalHeader()->setSectionResizeMode(0, QHeaderView::Fixed);
    _tblNotePreview->setColumnWidth(0, 52);
    _tblNotePreview->horizontalHeader()->setSectionResizeMode(1, QHeaderView::Fixed);
    _tblNotePreview->setColumnWidth(1, 82);
    _tblNotePreview->horizontalHeader()->setSectionResizeMode(2, QHeaderView::Fixed);
    _tblNotePreview->setColumnWidth(2, 86);
    _tblNotePreview->horizontalHeader()->setSectionResizeMode(3, QHeaderView::Interactive);
    _tblNotePreview->setColumnWidth(3, 130);
    _tblNotePreview->horizontalHeader()->setSectionResizeMode(4, QHeaderView::Stretch);

    connect(_tblNotePreview, &QTableWidget::itemChanged, this, &GeneratorConfigDialog::onNotePreviewItemChanged);
    rightLayout->addWidget(_tblNotePreview, 1);

    outerLayout->addWidget(_notePreviewPanel, 1);
}

void GeneratorConfigDialog::UpdatePreviewPanelVisibility(bool visible) {
    _notePreviewPanel->setVisible(visible);
    if (visible) {
        setMinimumSize(880, 420);
        setMaximumSize(16777215, 16777215);
        resize(940, 460);
        RefreshNoteSourceCombo();
    } else {
        setMinimumSize(364, 420);
        setMaximumSize(364, 420);
        resize(364, 420);
    }
}

void GeneratorConfigDialog::onDuplicateNotesToggled(bool checked) { UpdatePreviewPanelVisibility(checked); }

void GeneratorConfigDialog::onParametersChanged() {
    if (chkDuplicateNotes && chkDuplicateNotes->isChecked()) {
        RebuildNotePreviewTable();
    }
}

bool GeneratorConfigDialog::ParseCopiesAndStride(int& outCopies, int& outStride) const {
    bool okC = false;
    int c = txtCopies ? txtCopies->text().toInt(&okC) : 0;
    if (!okC || c <= 0)
        return false;

    QString strideText = txtStride ? txtStride->text().trimmed() : "0";
    bool isHex = strideText.startsWith("0x", Qt::CaseInsensitive);
    if (isHex)
        strideText = strideText.mid(2);

    bool okS = false;
    int s = isHex ? strideText.toInt(&okS, 16) : strideText.toInt(&okS, 10);
    if (!okS)
        return false;

    outCopies = c;
    outStride = s;
    return true;
}

GeneratorConfigDialog::ResolvedSourceNote GeneratorConfigDialog::ResolveSourceInfoForAddress(const QString& addr) const {
    ResolvedSourceNote info;
    info.NormAddress = LogicFormatter::NormalizeAddress(addr);
    info.DisplayLabel = info.NormAddress;
    TryParseHex(info.NormAddress, info.SourceValue);

    if (_notes == nullptr || _notes->isEmpty())
        return info;

    PointerTreeParser parser;
    QMap<QString, CodeNote> notesMap;
    QMap<QString, QList<std::shared_ptr<NoteNode>>> parsedCache;
    QList<ConditionFormatter::RangeEntry> rangeCache;

    for (const auto& n : *_notes) {
        QString key = LogicFormatter::NormalizeAddress(n.Address);
        notesMap.insert(key, n);
        auto nodes = parser.ParseNoteText(n.Note);
        parsedCache.insert(key, nodes);
        if (!nodes.isEmpty() && nodes[0]->Size > 1) {
            int64_t start = 0;
            if (TryParseHex(key, start))
                rangeCache.append(ConditionFormatter::RangeEntry{start, start + nodes[0]->Size, nodes[0]});
        }
    }

    auto resolveRedirect = [&](const QString& startKey) -> QString {
        static const QRegularExpression structRx(QString::fromUtf8(R"(^[.\+\s\x{2500}-\x{257F}])"), QRegularExpression::MultilineOption);
        static const QRegularExpression referRx(R"(refer to \$?0x([0-9a-f]+))", QRegularExpression::CaseInsensitiveOption);
        QString cur = startKey;
        for (int hop = 0; hop < 5; ++hop) {
            if (!notesMap.contains(cur))
                break;
            const QString& txt = notesMap.value(cur).Note;
            if (structRx.match(txt).hasMatch())
                break;
            auto m = referRx.match(txt);
            if (!m.hasMatch())
                break;
            QString target = LogicFormatter::NormalizeAddress(m.captured(1));
            if (!notesMap.contains(target))
                break;
            cur = target;
        }
        return cur;
    };

    // 1. Check if addr belongs to an AddAddress chain in _sourceGroup
    if (_sourceGroup != nullptr) {
        QList<AchievementCondition> currentChain;
        for (const auto& cond : _sourceGroup->Conditions) {
            currentChain.append(cond);
            if (cond.Flag != "Add Address") {
                // Check if addr matches any step in this unit's LeftOperand chain
                int matchedStep = -1;
                for (int k = 0; k < currentChain.size(); ++k) {
                    if (RaLogicSyntax::MemoryTypes.contains(currentChain[k].LeftOperand.Type) &&
                        LogicFormatter::NormalizeAddress(currentChain[k].LeftOperand.Value) == info.NormAddress) {
                        matchedStep = k;
                        break;
                    }
                }

                if (matchedStep != -1 && currentChain.size() > 1) {
                    // Find true note-backed base AddAddress (skipping leading index AddAddress rows)
                    int baseIdx = -1;
                    for (int k = 0; k < currentChain.size() - 1; ++k) {
                        if (currentChain[k].Flag != "Add Address" || !RaLogicSyntax::MemoryTypes.contains(currentChain[k].LeftOperand.Type))
                            continue;
                        QString bKey = LogicFormatter::NormalizeAddress(currentChain[k].LeftOperand.Value);
                        int64_t bVal = 0;
                        TryParseHex(bKey, bVal);
                        bool hasNote = (notesMap.contains(bKey) && IsMeaningfulNote(notesMap.value(bKey).Note));
                        if (!hasNote) {
                            for (const auto& r : rangeCache) {
                                if (r.Node && !r.Node->IsPointerArray && bVal >= r.Start && bVal < r.End) {
                                    hasNote = true;
                                    break;
                                }
                            }
                        }
                        if (hasNote) {
                            baseIdx = k;
                            break;
                        }
                    }

                    if (baseIdx != -1 && matchedStep > baseIdx) {
                        QString baseKey = LogicFormatter::NormalizeAddress(currentChain[baseIdx].LeftOperand.Value);
                        QString resolvedBase = resolveRedirect(baseKey);
                        if (parsedCache.contains(resolvedBase)) {
                            QList<int64_t> offsets;
                            for (int k = baseIdx + 1; k <= matchedStep; ++k) {
                                int64_t off = 0;
                                TryParseHex(currentChain[k].LeftOperand.Value, off);
                                offsets.append(off);
                            }

                            std::shared_ptr<NoteNode> rootNode = nullptr;
                            QList<std::shared_ptr<NoteNode>> levelNodes = TopLevelNodesOf(parsedCache.value(resolvedBase), &rootNode);
                            std::shared_ptr<NoteNode> parentNode = rootNode;
                            std::shared_ptr<NoteNode> foundNode = nullptr;

                            for (int k = 0; k < offsets.size(); ++k) {
                                bool isLast = (k == offsets.size() - 1);
                                foundNode = ConditionFormatter::FindBestMatchingNode(levelNodes, offsets[k], isLast, nullptr, parentNode);
                                if (foundNode == nullptr)
                                    break;
                                if (foundNode->IsTemplateInherited) {
                                    if (auto realSrc = foundNode->TemplateSource.lock())
                                        foundNode = realSrc;
                                }
                                levelNodes = foundNode->Children;
                                parentNode = foundNode;
                            }

                            if (foundNode != nullptr && !foundNode->Description.trimmed().isEmpty()) {
                                info.IsStatic = false;
                                info.BaseNoteKey = resolvedBase;
                                info.ChainOffsets = offsets;
                                info.ChangedLevel = static_cast<int>(offsets.size()) - 1;
                                info.SourceHeaderText = foundNode->Description.trimmed();
                                info.DisplayLabel = QString("%1 -> %2").arg(resolvedBase, FormatHexTarget(offsets.last(), true));
                                return info;
                            }
                        }
                    }
                }
                currentChain.clear();
            }
        }
    }

    // 2. Standalone static CodeNote
    if (notesMap.contains(info.NormAddress) && IsMeaningfulNote(notesMap.value(info.NormAddress).Note)) {
        QString resolvedKey = resolveRedirect(info.NormAddress);
        const QString& noteText = notesMap.value(resolvedKey).Note;
        QString norm = noteText;
        norm.replace("\r\n", "\n").replace('\r', '\n');
        info.SourceHeaderText = norm.split('\n').first().trimmed();
        info.IsStatic = true;
        info.BaseNoteKey = resolvedKey;
        info.DisplayLabel = info.NormAddress;
        return info;
    }

    // 3. Range-contained static offset
    int64_t addrVal = info.SourceValue;
    const ConditionFormatter::RangeEntry* bestRange = nullptr;
    for (const auto& r : rangeCache) {
        if (r.Node && !r.Node->IsPointerArray && addrVal >= r.Start && addrVal < r.End) {
            if (bestRange == nullptr || r.Start > bestRange->Start ||
                (r.Start == bestRange->Start && (r.End - r.Start) < (bestRange->End - bestRange->Start))) {
                bestRange = &r;
            }
        }
    }
    if (bestRange != nullptr) {
        QString rangeKey = LogicFormatter::NormalizeAddress(QString("0x%1").arg(bestRange->Start, 0, 16));
        QString resolvedRange = resolveRedirect(rangeKey);
        QList<std::shared_ptr<NoteNode>> topNodes = TopLevelNodesOf(parsedCache.value(resolvedRange));
        auto matched = ConditionFormatter::FindBestMatchingNode(topNodes, addrVal - bestRange->Start, true);
        if (matched != nullptr) {
            if (matched->IsTemplateInherited) {
                if (auto realSrc = matched->TemplateSource.lock())
                    matched = realSrc;
            }
            if (!matched->Description.trimmed().isEmpty()) {
                info.SourceHeaderText = matched->Description.trimmed();
                info.IsStatic = true;
                info.BaseNoteKey = resolvedRange;
                return info;
            }
        }
    }

    return info;
}

QString GeneratorConfigDialog::CheckTargetStatus(const ResolvedSourceNote& src, int copyIndex, int stride,
                                                 QString& outExistingTitle) const {
    outExistingTitle.clear();
    if (_notes == nullptr)
        return "New";

    PointerTreeParser parser;
    if (src.IsStatic) {
        int64_t targetVal = src.SourceValue + static_cast<int64_t>(copyIndex) * stride;
        QString targetKey = LogicFormatter::NormalizeAddress(QString("0x%1").arg(static_cast<quint64>(targetVal), 0, 16));
        for (const auto& n : *_notes) {
            if (LogicFormatter::NormalizeAddress(n.Address) == targetKey && IsMeaningfulNote(n.Note)) {
                QString norm = n.Note;
                norm.replace("\r\n", "\n").replace('\r', '\n');
                outExistingTitle = norm.split('\n').first().trimmed();
                return "Exists (Skip)";
            }
        }
        return "New";
    }

    if (src.BaseNoteKey.isEmpty() || src.ChangedLevel < 0 || src.ChangedLevel >= src.ChainOffsets.size())
        return "New";

    QList<int64_t> dstOffsets = src.ChainOffsets;
    dstOffsets[src.ChangedLevel] += static_cast<int64_t>(copyIndex) * stride;

    for (const auto& n : *_notes) {
        if (LogicFormatter::NormalizeAddress(n.Address) == src.BaseNoteKey) {
            auto parsedNodes = parser.ParseNoteText(n.Note);
            QList<std::shared_ptr<NoteNode>> levelNodes = TopLevelNodesOf(parsedNodes);
            std::shared_ptr<NoteNode> matched = nullptr;
            for (int step = 0; step < dstOffsets.size(); ++step) {
                std::shared_ptr<NoteNode> found = nullptr;
                for (const auto& node : levelNodes) {
                    if (node == nullptr || node->IsInlineArray)
                        continue;
                    int64_t nodeOff = 0;
                    if (TryParseHex(node->Offset, nodeOff) && nodeOff == dstOffsets[step]) {
                        if (!node->IsTemplateInherited) {
                            found = node;
                            break;
                        }
                        if (found == nullptr)
                            found = node;
                    }
                }
                if (found == nullptr)
                    return "New";
                matched = found;
                levelNodes = found->Children;
            }
            if (matched != nullptr &&
                (!matched->Description.trimmed().isEmpty() || !matched->Content.trimmed().isEmpty() || !matched->Children.isEmpty())) {
                outExistingTitle = matched->Description.trimmed();
                return "Exists (Skip)";
            }
            break;
        }
    }

    return "New";
}

void GeneratorConfigDialog::RefreshNoteSourceCombo() {
    if (_cmbNoteSource == nullptr || !_notePreviewPanel->isVisible())
        return;

    SelectedAddresses.clear();
    for (int i = 0; i < tvAddresses->topLevelItemCount(); i++)
        CollectCheckedAddresses(tvAddresses->topLevelItem(i));

    _resolvedSources.clear();
    for (const QString& addr : SelectedAddresses) {
        ResolvedSourceNote info = ResolveSourceInfoForAddress(addr);
        if (!info.SourceHeaderText.isEmpty())
            _resolvedSources.append(info);
    }

    QString prevAddr = _cmbNoteSource->currentData().toString();
    {
        QSignalBlocker blocker(_cmbNoteSource);
        _cmbNoteSource->clear();
        for (const auto& src : _resolvedSources) {
            QString previewDesc = src.SourceHeaderText;
            if (previewDesc.length() > 38)
                previewDesc = previewDesc.left(35) + "...";
            _cmbNoteSource->addItem(QString("%1 | %2").arg(src.DisplayLabel, previewDesc), src.NormAddress);
        }

        int restoreIdx = _cmbNoteSource->findData(prevAddr);
        if (restoreIdx != -1)
            _cmbNoteSource->setCurrentIndex(restoreIdx);
        else if (_cmbNoteSource->count() > 0)
            _cmbNoteSource->setCurrentIndex(0);
    }

    onNoteSourceChanged(_cmbNoteSource->currentIndex());
}

void GeneratorConfigDialog::onNoteSourceChanged(int index) {
    if (_txtNoteTemplate == nullptr || _tblNotePreview == nullptr)
        return;

    if (index < 0 || index >= _resolvedSources.size()) {
        QSignalBlocker blocker(_txtNoteTemplate);
        _txtNoteTemplate->clear();
        _txtNoteTemplate->setEnabled(false);
        _btnPasteNames->setEnabled(false);
        _tblNotePreview->setRowCount(0);
        return;
    }

    _txtNoteTemplate->setEnabled(true);
    _btnPasteNames->setEnabled(true);

    const auto& src = _resolvedSources[index];
    auto parsedTmpl = CodeNoteTemplateService::ParseTemplateFromNote(src.SourceHeaderText);

    if (!NoteOverrides.contains(src.NormAddress)) {
        NoteOverrideConfig cfg;
        cfg.HeaderTemplate = parsedTmpl.HeaderTemplate;
        NoteOverrides.insert(src.NormAddress, cfg);
    } else if (NoteOverrides[src.NormAddress].HeaderTemplate.isEmpty()) {
        NoteOverrides[src.NormAddress].HeaderTemplate = parsedTmpl.HeaderTemplate;
    }

    {
        QSignalBlocker blocker(_txtNoteTemplate);
        _txtNoteTemplate->setText(NoteOverrides[src.NormAddress].HeaderTemplate);
    }

    RebuildNotePreviewTable();
}

void GeneratorConfigDialog::onNoteTemplateChanged(const QString& text) {
    int idx = _cmbNoteSource ? _cmbNoteSource->currentIndex() : -1;
    if (idx < 0 || idx >= _resolvedSources.size())
        return;

    const QString& key = _resolvedSources[idx].NormAddress;
    NoteOverrides[key].HeaderTemplate = text;
    // Clear explicit manual title overrides when the user edits the shared template
    NoteOverrides[key].ExplicitTitles.clear();
    RebuildNotePreviewTable();
}

void GeneratorConfigDialog::RebuildNotePreviewTable() {
    if (_tblNotePreview == nullptr || !_notePreviewPanel->isVisible())
        return;

    int idx = _cmbNoteSource ? _cmbNoteSource->currentIndex() : -1;
    if (idx < 0 || idx >= _resolvedSources.size()) {
        _tblNotePreview->setRowCount(0);
        return;
    }

    int copies = 0;
    int stride = 0;
    if (!ParseCopiesAndStride(copies, stride)) {
        _tblNotePreview->setRowCount(0);
        return;
    }

    const int displayCopies = std::min(copies, 256);
    const auto& src = _resolvedSources[idx];
    auto parsedTmpl = CodeNoteTemplateService::ParseTemplateFromNote(src.SourceHeaderText);
    auto& overrideCfg = NoteOverrides[src.NormAddress];
    const QString activeTmpl =
        overrideCfg.HeaderTemplate.isEmpty() ? parsedTmpl.HeaderTemplate : overrideCfg.HeaderTemplate;

    _isUpdatingNotePreview = true;
    _tblNotePreview->setRowCount(displayCopies);

    const bool isArray = chkGenerateAsArray && chkGenerateAsArray->isChecked();

    for (int row = 0; row < displayCopies; ++row) {
        int copyIndex = row + 1;
        QString copyLabel = isArray ? QString("#%1").arg(copyIndex) : QString("Alt %1").arg(copyIndex);

        auto* copyItem = new QTableWidgetItem(copyLabel);
        copyItem->setFlags(copyItem->flags() & ~Qt::ItemIsEditable);
        copyItem->setTextAlignment(Qt::AlignCenter);
        _tblNotePreview->setItem(row, 0, copyItem);

        QString targetLabel;
        if (src.IsStatic) {
            targetLabel = FormatHexTarget(src.SourceValue + static_cast<int64_t>(copyIndex) * stride, false);
        } else if (src.ChangedLevel >= 0 && src.ChangedLevel < src.ChainOffsets.size()) {
            targetLabel = FormatHexTarget(src.ChainOffsets[src.ChangedLevel] + static_cast<int64_t>(copyIndex) * stride, true);
        }
        auto* targetItem = new QTableWidgetItem(targetLabel);
        targetItem->setFlags(targetItem->flags() & ~Qt::ItemIsEditable);
        targetItem->setTextAlignment(Qt::AlignCenter);
        _tblNotePreview->setItem(row, 1, targetItem);

        QString existingTitle;
        QString status = CheckTargetStatus(src, copyIndex, stride, existingTitle);
        bool exists = (status != "New");

        auto* statusItem = new QTableWidgetItem(status);
        statusItem->setFlags(statusItem->flags() & ~Qt::ItemIsEditable);
        statusItem->setTextAlignment(Qt::AlignCenter);
        statusItem->setForeground(exists ? QColor(220, 140, 0) : QColor(46, 160, 67));
        _tblNotePreview->setItem(row, 2, statusItem);

        QString customName = overrideCfg.CustomNames.contains(copyIndex) ? overrideCfg.CustomNames.value(copyIndex)
                                                                         : parsedTmpl.FirstCustomName;
        auto* nameItem = new QTableWidgetItem(customName);
        if (exists)
            nameItem->setFlags(nameItem->flags() & ~Qt::ItemIsEditable);
        _tblNotePreview->setItem(row, 3, nameItem);

        QString generatedTitle;
        if (exists) {
            generatedTitle = existingTitle;
        } else if (overrideCfg.ExplicitTitles.contains(copyIndex) && !overrideCfg.ExplicitTitles.value(copyIndex).isEmpty()) {
            generatedTitle = overrideCfg.ExplicitTitles.value(copyIndex);
        } else {
            uint32_t deltaBytes = static_cast<uint32_t>(std::llabs(static_cast<int64_t>(copyIndex) * stride));
            generatedTitle = CodeNoteTemplateService::ExpandTemplate(activeTmpl, parsedTmpl.BaseIndex + copyIndex, deltaBytes, customName);
        }

        auto* titleItem = new QTableWidgetItem(generatedTitle);
        if (exists) {
            titleItem->setFlags(titleItem->flags() & ~Qt::ItemIsEditable);
            titleItem->setForeground(Qt::gray);
        }
        _tblNotePreview->setItem(row, 4, titleItem);
    }

    _isUpdatingNotePreview = false;
}

void GeneratorConfigDialog::onNotePreviewItemChanged(QTableWidgetItem* item) {
    if (_isUpdatingNotePreview || item == nullptr)
        return;

    int idx = _cmbNoteSource ? _cmbNoteSource->currentIndex() : -1;
    if (idx < 0 || idx >= _resolvedSources.size())
        return;

    const auto& src = _resolvedSources[idx];
    auto& overrideCfg = NoteOverrides[src.NormAddress];

    int row = item->row();
    int col = item->column();
    int copyIndex = row + 1;

    if (col == 3) {
        overrideCfg.CustomNames.insert(copyIndex, item->text());
        overrideCfg.ExplicitTitles.remove(copyIndex);

        int copies = 0;
        int stride = 0;
        if (ParseCopiesAndStride(copies, stride)) {
            auto parsedTmpl = CodeNoteTemplateService::ParseTemplateFromNote(src.SourceHeaderText);
            QString activeTmpl =
                overrideCfg.HeaderTemplate.isEmpty() ? parsedTmpl.HeaderTemplate : overrideCfg.HeaderTemplate;
            uint32_t deltaBytes = static_cast<uint32_t>(std::llabs(static_cast<int64_t>(copyIndex) * stride));
            QString formatted =
                CodeNoteTemplateService::ExpandTemplate(activeTmpl, parsedTmpl.BaseIndex + copyIndex, deltaBytes, item->text());

            _isUpdatingNotePreview = true;
            if (auto* titleItem = _tblNotePreview->item(row, 4))
                titleItem->setText(formatted);
            _isUpdatingNotePreview = false;
        }
    } else if (col == 4) {
        overrideCfg.ExplicitTitles.insert(copyIndex, item->text().trimmed());
    }
}

void GeneratorConfigDialog::onPasteNamesClicked() {
    int idx = _cmbNoteSource ? _cmbNoteSource->currentIndex() : -1;
    if (idx < 0 || idx >= _resolvedSources.size() || _tblNotePreview == nullptr)
        return;

    QString text = QApplication::clipboard()->text();
    if (text.trimmed().isEmpty())
        return;

    QStringList lines = text.split(QRegularExpression("[\r\n]+"), Qt::SkipEmptyParts);
    if (lines.isEmpty())
        return;

    const auto& src = _resolvedSources[idx];
    auto& overrideCfg = NoteOverrides[src.NormAddress];
    auto parsedTmpl = CodeNoteTemplateService::ParseTemplateFromNote(src.SourceHeaderText);
    QString activeTmpl = overrideCfg.HeaderTemplate.isEmpty() ? parsedTmpl.HeaderTemplate : overrideCfg.HeaderTemplate;

    // Auto-inject ({NAME}) or {NAME} into template if the user hasn't placed it yet
    if (!activeTmpl.contains("{NAME}")) {
        activeTmpl += " ({NAME})";
        overrideCfg.HeaderTemplate = activeTmpl;
        QSignalBlocker blocker(_txtNoteTemplate);
        _txtNoteTemplate->setText(activeTmpl);
    }

    QList<int> newRows;
    for (int r = 0; r < _tblNotePreview->rowCount(); ++r) {
        auto* statusItem = _tblNotePreview->item(r, 2);
        if (statusItem != nullptr && statusItem->text() == "New")
            newRows.append(r);
    }

    // If clipboard matches the count of New rows (when some rows already exist), paste onto New rows directly
    bool targetNewRowsOnly = (!newRows.isEmpty() && newRows.size() < _tblNotePreview->rowCount() && lines.size() <= newRows.size());
    int applyCount = targetNewRowsOnly ? static_cast<int>(lines.size()) : std::min(static_cast<int>(lines.size()), _tblNotePreview->rowCount());

    for (int k = 0; k < applyCount; ++k) {
        int row = targetNewRowsOnly ? newRows[k] : k;
        int copyIndex = row + 1;
        overrideCfg.CustomNames.insert(copyIndex, lines[k].trimmed());
        overrideCfg.ExplicitTitles.remove(copyIndex);
    }

    RebuildNotePreviewTable();
}

void GeneratorConfigDialog::InitializeTreeView(const QList<std::shared_ptr<GeneratorItemViewModel>>& items) {
    tvAddresses->clear();
    for (const auto& item : items) {
        if (item != nullptr)
            tvAddresses->addTopLevelItem(CreateTreeNode(*item));
    }
    tvAddresses->expandAll();
}

QTreeWidgetItem* GeneratorConfigDialog::CreateTreeNode(const GeneratorItemViewModel& item) {
    auto* node = new QTreeWidgetItem();
    node->setText(0, item.ToString());
    node->setData(0, Qt::UserRole, item.Address); // Store raw address in Tag for retrieval
    node->setFlags(node->flags() | Qt::ItemIsUserCheckable);
    node->setCheckState(0, Qt::Unchecked);

    for (const auto& child : item.Children) {
        if (child != nullptr)
            node->addChild(CreateTreeNode(*child));
    }
    return node;
}

void GeneratorConfigDialog::CheckNodesRecursive(QTreeWidgetItem* node, const QSet<QString>& addressesToCheck) {
    if (node == nullptr)
        return;

    QString addr = node->data(0, Qt::UserRole).toString();
    if (!addr.isEmpty()) {
        // Normalize both sides to ensure matches (e.g. 0x1234 vs 0x001234)
        QString normAddr = addr.startsWith("0x", Qt::CaseInsensitive) ? addr.mid(2) : addr;
        while (normAddr.startsWith('0') && normAddr.length() > 1)
            normAddr = normAddr.mid(1);
        if (normAddr.isEmpty())
            normAddr = "0";

        bool match = false;
        for (const QString& checkAddr : addressesToCheck) {
            QString normCheck = checkAddr.startsWith("0x", Qt::CaseInsensitive) ? checkAddr.mid(2) : checkAddr;
            while (normCheck.startsWith('0') && normCheck.length() > 1)
                normCheck = normCheck.mid(1);
            if (normCheck.isEmpty())
                normCheck = "0";

            if (normAddr.compare(normCheck, Qt::CaseInsensitive) == 0) {
                match = true;
                break;
            }
        }

        if (match) {
            node->setCheckState(0, Qt::Checked);
            // Ensure parents are expanded to show checked items
            auto* parent = node->parent();
            while (parent != nullptr) {
                parent->setExpanded(true);
                parent = parent->parent();
            }
        }
    }

    for (int i = 0; i < node->childCount(); i++)
        CheckNodesRecursive(node->child(i), addressesToCheck);
}

// Cascade checking to children
void GeneratorConfigDialog::onTreeItemChanged(QTreeWidgetItem* item, int column) {
    // Only process user actions or code triggers, avoid infinite recursion loops by checking strict action if needed
    // But simple recursion is fine here as long as we don't re-trigger for same state
    if (_isUpdatingCheck || column != 0 || item == nullptr)
        return;

    _isUpdatingCheck = true;
    bool isChecked = (item->checkState(0) == Qt::Checked);
    SetChildrenChecked(item, isChecked);
    _isUpdatingCheck = false;

    if (chkDuplicateNotes && chkDuplicateNotes->isChecked())
        RefreshNoteSourceCombo();
}

void GeneratorConfigDialog::SetChildrenChecked(QTreeWidgetItem* item, bool isChecked) {
    for (int i = 0; i < item->childCount(); i++) {
        auto* child = item->child(i);
        child->setCheckState(0, isChecked ? Qt::Checked : Qt::Unchecked);
        SetChildrenChecked(child, isChecked);
    }
}

void GeneratorConfigDialog::onSelectAll() {
    bool anyUnchecked = false;
    for (int i = 0; i < tvAddresses->topLevelItemCount(); i++) {
        if (!IsNodeTreeChecked(tvAddresses->topLevelItem(i))) {
            anyUnchecked = true;
            break;
        }
    }

    _isUpdatingCheck = true;
    bool newState = anyUnchecked;
    for (int i = 0; i < tvAddresses->topLevelItemCount(); i++) {
        auto* node = tvAddresses->topLevelItem(i);
        node->setCheckState(0, newState ? Qt::Checked : Qt::Unchecked);
        SetChildrenChecked(node, newState);
    }
    _isUpdatingCheck = false;

    if (chkDuplicateNotes && chkDuplicateNotes->isChecked())
        RefreshNoteSourceCombo();
}

bool GeneratorConfigDialog::IsNodeTreeChecked(QTreeWidgetItem* item) const {
    if (item->checkState(0) != Qt::Checked)
        return false;
    for (int i = 0; i < item->childCount(); i++) {
        if (!IsNodeTreeChecked(item->child(i)))
            return false;
    }
    return true;
}

void GeneratorConfigDialog::onBtnOK() {
    // Validate Copies
    bool ok = false;
    int c = txtCopies->text().toInt(&ok);
    if (!ok || c <= 0) {
        QMessageBox::warning(this, "Input Error", "Please enter a valid number of copies (1 or more).");
        return;
    }

    // Validate Stride
    QString strideText = txtStride->text().trimmed();
    bool isHex = strideText.startsWith("0x", Qt::CaseInsensitive);
    if (isHex)
        strideText = strideText.mid(2);

    int s = isHex ? strideText.toInt(&ok, 16) : strideText.toInt(&ok, 10);
    if (!ok) {
        QMessageBox::warning(this, "Input Error", "Please enter a valid stride (Integers or 0xHex).");
        return;
    }

    // Collect selected addresses from tree
    SelectedAddresses.clear();
    for (int i = 0; i < tvAddresses->topLevelItemCount(); i++)
        CollectCheckedAddresses(tvAddresses->topLevelItem(i));

    // Validate Selection
    if (SelectedAddresses.isEmpty()) {
        QMessageBox::warning(this, "Input Error", "Please select at least one address to increment.");
        return;
    }

    Copies = c;
    Stride = s;
    IsArray = chkGenerateAsArray->isChecked();
    DuplicateSelectedOnly = IsArray && chkDuplicateSelectedOnly->isChecked();
    DuplicateNotes = chkDuplicateNotes->isChecked();

    // Save state for next use
    AppSettings::Current.Generator_Copies = txtCopies->text();
    AppSettings::Current.Generator_Stride = txtStride->text();
    AppSettings::Current.Generator_IsArray = chkGenerateAsArray->isChecked();
    AppSettings::Current.Generator_DuplicateSelectedOnly = chkDuplicateSelectedOnly->isChecked();
    AppSettings::Current.Generator_DuplicateNotes = chkDuplicateNotes->isChecked();
    AppSettings::Save();
    _userModifiedStride = true; // Mark as explicitly set by user

    accept();
}

void GeneratorConfigDialog::CollectCheckedAddresses(QTreeWidgetItem* node) {
    if (node == nullptr)
        return;
    if (node->checkState(0) == Qt::Checked) {
        QString addr = node->data(0, Qt::UserRole).toString();
        if (!addr.isEmpty() && !SelectedAddresses.contains(addr))
            SelectedAddresses.append(addr);
    }
    for (int i = 0; i < node->childCount(); i++)
        CollectCheckedAddresses(node->child(i));
}

void GeneratorConfigDialog::onBtnCancel() { reject(); }

} // namespace RALE
