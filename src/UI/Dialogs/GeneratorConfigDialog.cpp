#include "GeneratorConfigDialog.h"
#include "../../Common/AppSettings.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QFormLayout>
#include <QLabel>
#include <QMessageBox>
#include <QRegularExpression>

namespace RALE {

bool GeneratorConfigDialog::_userModifiedStride = false; // Tracks if the user has explicitly set a stride this session

QString GeneratorConfigDialog::GeneratorItemViewModel::ToString() const
{
    if (Description.isEmpty()) return Address;

    // Strip brackets [32-bit] etc for cleaner display, as requested
    static const QRegularExpression bracketRegex(R"(\[.*?\])");
    QString cleanDesc = Description;
    cleanDesc.remove(bracketRegex);
    cleanDesc = cleanDesc.trimmed();

    if (cleanDesc.isEmpty()) return Address;
    return QString("%1 (%2)").arg(Address, cleanDesc);
}

// Constructor now accepts a hierarchical list of items and pre-selected addresses
GeneratorConfigDialog::GeneratorConfigDialog(
    const QList<std::shared_ptr<GeneratorItemViewModel>> &rootItems,
    int defaultStride,
    const GeneratorDefinition *existingDef,
    const QSet<QString> *preSelectedAddresses,
    QWidget *parent)
    : QDialog(parent)
{
    InitializeUI();
    InitializeTreeView(rootItems);

    // 1. Apply Persistence Defaults First
    txtCopies->setText(AppSettings::Current.Generator_Copies);
    txtStride->setText(AppSettings::Current.Generator_Stride);
    chkGenerateAsArray->setChecked(AppSettings::Current.Generator_IsArray);

    // 2. Pre-fill Stride if auto-detected (Only override if the user hasn't explicitly customized it this session)
    if (defaultStride > 0 && !_userModifiedStride)
    {
        txtStride->setText("0x" + QString::number(defaultStride, 16).toUpper());
    }

    // 3. Determine Checkbox States
    if (existingDef != nullptr && !existingDef->Rules.isEmpty())
    {
        // If we are editing an existing generator, override with its stored definition
        txtCopies->setText(QString::number(existingDef->Copies));
        chkGenerateAsArray->setChecked(existingDef->IsArray);

        int foundStride = existingDef->Rules[0].Stride;
        txtStride->setText("0x" + QString::number(foundStride, 16).toUpper());

        // Check addresses that are already in the rules
        QSet<QString> rulesSet;
        for (const auto &r : existingDef->Rules)
            rulesSet.insert(r.SourceAddress);

        _isUpdatingCheck = true;
        for (int i = 0; i < tvAddresses->topLevelItemCount(); i++)
            CheckNodesRecursive(tvAddresses->topLevelItem(i), rulesSet);
        _isUpdatingCheck = false;
    }
    else if (preSelectedAddresses != nullptr && !preSelectedAddresses->isEmpty())
    {
        if (existingDef != nullptr)
        {
            txtCopies->setText(QString::number(existingDef->Copies));
            chkGenerateAsArray->setChecked(existingDef->IsArray);
        }

        // Pre-check nodes based on what the user had selected in the Editor Grid
        _isUpdatingCheck = true;
        for (int i = 0; i < tvAddresses->topLevelItemCount(); i++)
            CheckNodesRecursive(tvAddresses->topLevelItem(i), *preSelectedAddresses);
        _isUpdatingCheck = false;
    }
    else if (tvAddresses->topLevelItemCount() == 1)
    {
        if (existingDef != nullptr)
        {
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

    loadPositionSettings();
}

void GeneratorConfigDialog::loadPositionSettings()
{
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("GeneratorConfigDialog", p, s))
    {
        move(p);
    }
}

void GeneratorConfigDialog::savePositionSettings()
{
    AppSettings::SaveDialogGeometry("GeneratorConfigDialog", pos());
}

void GeneratorConfigDialog::done(int r)
{
    savePositionSettings();
    QDialog::done(r);
}

void GeneratorConfigDialog::InitializeUI()
{
    setWindowTitle("Create Generator");
    setFixedSize(364, 370);

    auto *mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(12, 9, 12, 10);
    mainLayout->setSpacing(5);

    auto *lblInstructions = new QLabel("Select Base Addresses to Expand:", this);
    QFont f = lblInstructions->font();
    f.setBold(true);
    lblInstructions->setFont(f);
    mainLayout->addWidget(lblInstructions);

    tvAddresses = new QTreeWidget(this);
    tvAddresses->setObjectName("tvAddresses");
    tvAddresses->setHeaderHidden(true);
    tvAddresses->setAnimated(false);
    tvAddresses->setIndentation(19);
    tvAddresses->setRootIsDecorated(true);
    connect(tvAddresses, &QTreeWidget::itemChanged, this, &GeneratorConfigDialog::onTreeItemChanged);
    mainLayout->addWidget(tvAddresses, 1);

    auto *topBtnRow = new QHBoxLayout();
    topBtnRow->setContentsMargins(0, 0, 0, 0);
    topBtnRow->addStretch();
    btnSelectAll = new QPushButton("Select All", this);
    btnSelectAll->setFixedSize(75, 23);
    connect(btnSelectAll, &QPushButton::clicked, this, &GeneratorConfigDialog::onSelectAll);
    topBtnRow->addWidget(btnSelectAll);
    mainLayout->addLayout(topBtnRow);

    auto *formLayout = new QFormLayout();
    formLayout->setContentsMargins(0, 2, 0, 0);
    formLayout->setSpacing(5);
    formLayout->setLabelAlignment(Qt::AlignLeft);

    txtCopies = new QLineEdit("1", this);
    txtCopies->setFixedSize(100, 23);
    formLayout->addRow("Number of Copies:", txtCopies);

    txtStride = new QLineEdit("0x0", this);
    txtStride->setFixedSize(100, 23);
    formLayout->addRow("Stride (Address):", txtStride);

    auto *lblStrideHint = new QLabel("Hex supported (0x)", this);
    lblStrideHint->setObjectName("lblStrideHint");
    formLayout->addRow("", lblStrideHint);

    mainLayout->addLayout(formLayout);

    chkGenerateAsArray = new QCheckBox("Generate as Array (Append to this Group, don't create Alts)", this);
    mainLayout->addWidget(chkGenerateAsArray);

    auto *btnRow = new QHBoxLayout();
    btnRow->setContentsMargins(0, 6, 0, 0);
    btnRow->addStretch();
    btnOK = new QPushButton("OK", this);
    btnOK->setFixedSize(75, 23);
    connect(btnOK, &QPushButton::clicked, this, &GeneratorConfigDialog::onBtnOK);
    btnCancel = new QPushButton("Cancel", this);
    btnCancel->setFixedSize(75, 23);
    connect(btnCancel, &QPushButton::clicked, this, &GeneratorConfigDialog::onBtnCancel);
    btnRow->addWidget(btnOK);
    btnRow->addWidget(btnCancel);
    mainLayout->addLayout(btnRow);
}

void GeneratorConfigDialog::InitializeTreeView(const QList<std::shared_ptr<GeneratorItemViewModel>> &items)
{
    tvAddresses->clear();
    for (const auto &item : items)
    {
        if (item != nullptr)
            tvAddresses->addTopLevelItem(CreateTreeNode(*item));
    }
    tvAddresses->expandAll();
}

QTreeWidgetItem *GeneratorConfigDialog::CreateTreeNode(const GeneratorItemViewModel &item)
{
    auto *node = new QTreeWidgetItem();
    node->setText(0, item.ToString());
    node->setData(0, Qt::UserRole, item.Address); // Store raw address in Tag for retrieval
    node->setFlags(node->flags() | Qt::ItemIsUserCheckable);
    node->setCheckState(0, Qt::Unchecked);

    for (const auto &child : item.Children)
    {
        if (child != nullptr)
            node->addChild(CreateTreeNode(*child));
    }
    return node;
}

void GeneratorConfigDialog::CheckNodesRecursive(QTreeWidgetItem *node, const QSet<QString> &addressesToCheck)
{
    if (node == nullptr) return;

    QString addr = node->data(0, Qt::UserRole).toString();
    if (!addr.isEmpty())
    {
        // Normalize both sides to ensure matches (e.g. 0x1234 vs 0x001234)
        QString normAddr = addr.startsWith("0x", Qt::CaseInsensitive) ? addr.mid(2) : addr;
        while (normAddr.startsWith('0') && normAddr.length() > 1) normAddr = normAddr.mid(1);
        if (normAddr.isEmpty()) normAddr = "0";

        bool match = false;
        for (const QString &checkAddr : addressesToCheck)
        {
            QString normCheck = checkAddr.startsWith("0x", Qt::CaseInsensitive) ? checkAddr.mid(2) : checkAddr;
            while (normCheck.startsWith('0') && normCheck.length() > 1) normCheck = normCheck.mid(1);
            if (normCheck.isEmpty()) normCheck = "0";

            if (normAddr.compare(normCheck, Qt::CaseInsensitive) == 0)
            {
                match = true;
                break;
            }
        }

        if (match)
        {
            node->setCheckState(0, Qt::Checked);
            // Ensure parents are expanded to show checked items
            auto *parent = node->parent();
            while (parent != nullptr)
            {
                parent->setExpanded(true);
                parent = parent->parent();
            }
        }
    }

    for (int i = 0; i < node->childCount(); i++)
        CheckNodesRecursive(node->child(i), addressesToCheck);
}

// Cascade checking to children
void GeneratorConfigDialog::onTreeItemChanged(QTreeWidgetItem *item, int column)
{
    // Only process user actions or code triggers, avoid infinite recursion loops by checking strict action if needed
    // But simple recursion is fine here as long as we don't re-trigger for same state
    if (_isUpdatingCheck || column != 0 || item == nullptr) return;

    _isUpdatingCheck = true;
    bool isChecked = (item->checkState(0) == Qt::Checked);
    SetChildrenChecked(item, isChecked);
    _isUpdatingCheck = false;
}

void GeneratorConfigDialog::SetChildrenChecked(QTreeWidgetItem *item, bool isChecked)
{
    for (int i = 0; i < item->childCount(); i++)
    {
        auto *child = item->child(i);
        child->setCheckState(0, isChecked ? Qt::Checked : Qt::Unchecked);
        SetChildrenChecked(child, isChecked);
    }
}

void GeneratorConfigDialog::onSelectAll()
{
    bool anyUnchecked = false;
    for (int i = 0; i < tvAddresses->topLevelItemCount(); i++)
    {
        if (!IsNodeTreeChecked(tvAddresses->topLevelItem(i)))
        {
            anyUnchecked = true;
            break;
        }
    }

    _isUpdatingCheck = true;
    bool newState = anyUnchecked;
    for (int i = 0; i < tvAddresses->topLevelItemCount(); i++)
    {
        auto *node = tvAddresses->topLevelItem(i);
        node->setCheckState(0, newState ? Qt::Checked : Qt::Unchecked);
        SetChildrenChecked(node, newState);
    }
    _isUpdatingCheck = false;
}

bool GeneratorConfigDialog::IsNodeTreeChecked(QTreeWidgetItem *item) const
{
    if (item->checkState(0) != Qt::Checked) return false;
    for (int i = 0; i < item->childCount(); i++)
    {
        if (!IsNodeTreeChecked(item->child(i))) return false;
    }
    return true;
}

void GeneratorConfigDialog::onBtnOK()
{
    // Validate Copies
    bool ok = false;
    int c = txtCopies->text().toInt(&ok);
    if (!ok || c <= 0)
    {
        QMessageBox::warning(this, "Input Error", "Please enter a valid number of copies (1 or more).");
        return;
    }

    // Validate Stride
    QString strideText = txtStride->text().trimmed();
    bool isHex = strideText.startsWith("0x", Qt::CaseInsensitive);
    if (isHex) strideText = strideText.mid(2);

    int s = isHex ? strideText.toInt(&ok, 16) : strideText.toInt(&ok, 10);
    if (!ok)
    {
        QMessageBox::warning(this, "Input Error", "Please enter a valid stride (Integers or 0xHex).");
        return;
    }

    // Collect selected addresses from tree
    SelectedAddresses.clear();
    for (int i = 0; i < tvAddresses->topLevelItemCount(); i++)
        CollectCheckedAddresses(tvAddresses->topLevelItem(i));

    // Validate Selection
    if (SelectedAddresses.isEmpty())
    {
        QMessageBox::warning(this, "Input Error", "Please select at least one address to increment.");
        return;
    }

    Copies = c;
    Stride = s;
    IsArray = chkGenerateAsArray->isChecked();

    // Save state for next use
    AppSettings::Current.Generator_Copies = txtCopies->text();
    AppSettings::Current.Generator_Stride = txtStride->text();
    AppSettings::Current.Generator_IsArray = chkGenerateAsArray->isChecked();
    AppSettings::Save();
    _userModifiedStride = true; // Mark as explicitly set by user

    accept();
}

void GeneratorConfigDialog::CollectCheckedAddresses(QTreeWidgetItem *node)
{
    if (node == nullptr) return;
    if (node->checkState(0) == Qt::Checked)
    {
        QString addr = node->data(0, Qt::UserRole).toString();
        if (!addr.isEmpty() && !SelectedAddresses.contains(addr))
            SelectedAddresses.append(addr);
    }
    for (int i = 0; i < node->childCount(); i++)
        CollectCheckedAddresses(node->child(i));
}

void GeneratorConfigDialog::onBtnCancel()
{
    reject();
}

} // namespace RALE
