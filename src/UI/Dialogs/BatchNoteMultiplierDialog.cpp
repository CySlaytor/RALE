#include "BatchNoteMultiplierDialog.h"
#include "../../Common/AppSettings.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QSplitter>
#include <QGroupBox>
#include <QFormLayout>
#include <QLabel>
#include <QMessageBox>
#include <algorithm>

namespace RALE {

BatchNoteMultiplierDialog::BatchNoteMultiplierDialog(const QList<CodeNote> &allNotes, QWidget *parent)
    : QDialog(parent), _allNotes(allNotes)
{
    InitializeUI();

    // Load persisted state
    txtCopies->setText(QString::number(AppSettings::Current.BatchMultiply_Copies));
    txtStride->setText(AppSettings::Current.BatchMultiply_Stride);
    txtStartNum->setText(QString::number(AppSettings::Current.BatchMultiply_StartNum));
    txtSearchKeyword->setText(AppSettings::Current.BatchMultiply_Search);
    txtReplacePattern->setText(AppSettings::Current.BatchMultiply_Replace);

    PopulateList("");
    loadPositionSettings();
}

void BatchNoteMultiplierDialog::loadPositionSettings()
{
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("BatchNoteMultiplierDialog", p, s))
    {
        move(p);
        if (s.isValid())
        {
            resize(std::max(s.width(), 600), std::max(s.height(), 350));
        }
    }
}

void BatchNoteMultiplierDialog::savePositionSettings()
{
    AppSettings::SaveDialogGeometry("BatchNoteMultiplierDialog", pos(), size());
}

void BatchNoteMultiplierDialog::done(int r)
{
    savePositionSettings();
    QDialog::done(r);
}

void BatchNoteMultiplierDialog::InitializeUI()
{
    setWindowTitle("Batch Note Multiplier");
    resize(684, 361);
    setMinimumSize(600, 350);

    auto *mainLayout = new QHBoxLayout(this);
    mainLayout->setContentsMargins(0, 0, 0, 0);

    auto *splitMain = new QSplitter(Qt::Horizontal, this);

    // Left Panel: Notes list
    auto *leftPanel = new QWidget(splitMain);
    auto *leftLayout = new QVBoxLayout(leftPanel);
    leftLayout->setContentsMargins(5, 5, 5, 5);
    leftLayout->setSpacing(4);

    // Header panel matching C# panelFilter (height 55px)
    auto *panelFilter = new QWidget(leftPanel);
    panelFilter->setFixedHeight(55);
    auto *filterLayout = new QVBoxLayout(panelFilter);
    filterLayout->setContentsMargins(0, 0, 0, 0);
    filterLayout->setSpacing(3);

    auto *filterRow1 = new QHBoxLayout();
    filterRow1->setContentsMargins(0, 0, 0, 0);
    auto *lblFilter = new QLabel("Filter Notes:", panelFilter);
    lblFilter->setFixedWidth(75);
    filterRow1->addWidget(lblFilter);
    txtFilter = new QLineEdit(panelFilter);
    txtFilter->setFixedHeight(23);
    connect(txtFilter, &QLineEdit::textChanged, this, &BatchNoteMultiplierDialog::onFilterChanged);
    filterRow1->addWidget(txtFilter, 1);
    filterLayout->addLayout(filterRow1);

    auto *filterRow2 = new QHBoxLayout();
    filterRow2->setContentsMargins(0, 0, 0, 0);
    filterRow2->addStretch();
    btnSelectAll = new QPushButton("Select All", panelFilter);
    btnSelectAll->setFixedSize(75, 23);
    connect(btnSelectAll, &QPushButton::clicked, this, &BatchNoteMultiplierDialog::onSelectAll);
    filterRow2->addWidget(btnSelectAll);

    btnSelectNone = new QPushButton("Select None", panelFilter);
    btnSelectNone->setFixedSize(85, 23);
    connect(btnSelectNone, &QPushButton::clicked, this, &BatchNoteMultiplierDialog::onSelectNone);
    filterRow2->addWidget(btnSelectNone);
    filterLayout->addLayout(filterRow2);

    leftLayout->addWidget(panelFilter);

    clbNotes = new QListWidget(leftPanel);
    clbNotes->setObjectName("clbNotes");
    leftLayout->addWidget(clbNotes, 1);
    splitMain->addWidget(leftPanel);

    // Right Panel: Settings
    auto *rightPanel = new QWidget(splitMain);
    auto *rightLayout = new QVBoxLayout(rightPanel);
    rightLayout->setContentsMargins(5, 5, 5, 5);
    rightLayout->setSpacing(6);

    auto *groupBox = new QGroupBox("Multiplication Settings", rightPanel);
    auto *formLayout = new QFormLayout(groupBox);
    formLayout->setContentsMargins(12, 10, 12, 10);
    formLayout->setSpacing(5);
    formLayout->setLabelAlignment(Qt::AlignLeft);

    txtCopies = new QLineEdit("1", groupBox);
    txtCopies->setFixedSize(150, 23);
    formLayout->addRow("Copies:", txtCopies);

    txtStride = new QLineEdit("0x0", groupBox);
    txtStride->setFixedSize(150, 23);
    formLayout->addRow("Stride (Hex):", txtStride);

    txtStartNum = new QLineEdit("2", groupBox);
    txtStartNum->setFixedSize(150, 23);
    formLayout->addRow("Starting Number:", txtStartNum);

    txtSearchKeyword = new QLineEdit(groupBox);
    txtSearchKeyword->setFixedSize(150, 23);
    formLayout->addRow("Search Keyword:", txtSearchKeyword);

    txtReplacePattern = new QLineEdit(groupBox);
    txtReplacePattern->setFixedSize(150, 23);
    formLayout->addRow("Replace With:", txtReplacePattern);

    auto *lblHint = new QLabel("(Use {N} for slot number)", groupBox);
    lblHint->setObjectName("lblHint");
    formLayout->addRow("", lblHint);

    rightLayout->addWidget(groupBox);
    rightLayout->addStretch();

    auto *actionBtnRow = new QHBoxLayout();
    actionBtnRow->addStretch();
    btnOK = new QPushButton("OK", rightPanel);
    btnOK->setFixedSize(75, 25);
    connect(btnOK, &QPushButton::clicked, this, &BatchNoteMultiplierDialog::onBtnOK);
    actionBtnRow->addWidget(btnOK);
    btnCancel = new QPushButton("Cancel", rightPanel);
    btnCancel->setFixedSize(75, 25);
    connect(btnCancel, &QPushButton::clicked, this, &BatchNoteMultiplierDialog::onBtnCancel);
    actionBtnRow->addWidget(btnCancel);
    rightLayout->addLayout(actionBtnRow);

    splitMain->addWidget(rightPanel);
    splitMain->setSizes({ 384, 290 });

    mainLayout->addWidget(splitMain);
}

void BatchNoteMultiplierDialog::onFilterChanged(const QString &filter)
{
    PopulateList(filter);

    // Auto-fill the replacement Search Keyword if the user is typing a filter
    if (!filter.trimmed().isEmpty() && txtSearchKeyword->text().trimmed().isEmpty())
    {
        txtSearchKeyword->setText(filter.trimmed());
    }
}

void BatchNoteMultiplierDialog::PopulateList(const QString &filter)
{
    clbNotes->clear();
    _filteredNotes.clear();

    bool autoCheck = !filter.trimmed().isEmpty();

    for (const auto &note : _allNotes)
    {
        QString header = note.Note.split('\n', Qt::SkipEmptyParts).value(0, "").trimmed();

        if (filter.trimmed().isEmpty() || header.contains(filter, Qt::CaseInsensitive))
        {
            _filteredNotes.append(note);
            auto *item = new QListWidgetItem(QString("%1 | %2").arg(note.Address, header), clbNotes);
            item->setFlags(item->flags() | Qt::ItemIsUserCheckable);
            item->setCheckState(autoCheck ? Qt::Checked : Qt::Unchecked);
        }
    }
}

void BatchNoteMultiplierDialog::onSelectAll()
{
    for (int i = 0; i < clbNotes->count(); i++)
        clbNotes->item(i)->setCheckState(Qt::Checked);
}

void BatchNoteMultiplierDialog::onSelectNone()
{
    for (int i = 0; i < clbNotes->count(); i++)
        clbNotes->item(i)->setCheckState(Qt::Unchecked);
}

void BatchNoteMultiplierDialog::onBtnOK()
{
    // Validate Selection
    QList<int> checkedIndices;
    for (int i = 0; i < clbNotes->count(); i++)
    {
        if (clbNotes->item(i)->checkState() == Qt::Checked)
            checkedIndices.append(i);
    }

    if (checkedIndices.isEmpty())
    {
        QMessageBox::warning(this, "No Selection", "Please select at least one Code Note to duplicate.");
        return;
    }

    // Validate Copies
    bool ok = false;
    int copies = txtCopies->text().toInt(&ok);
    if (!ok || copies <= 0)
    {
        QMessageBox::warning(this, "Input Error", "Please enter a valid number of copies (1 or more).");
        return;
    }

    // Validate Stride
    QString strideText = txtStride->text().trimmed();
    bool isHex = strideText.startsWith("0x", Qt::CaseInsensitive);
    if (isHex) strideText = strideText.mid(2);

    int64_t stride = isHex ? strideText.toLongLong(&ok, 16) : strideText.toLongLong(&ok, 10);
    if (!ok)
    {
        QMessageBox::warning(this, "Input Error", "Please enter a valid stride (Integers or 0xHex).");
        return;
    }

    // Validate Start Num
    int startNum = txtStartNum->text().toInt(&ok);
    if (!ok)
    {
        QMessageBox::warning(this, "Input Error", "Please enter a valid Starting Number.");
        return;
    }

    // Collect Data
    SelectedNotes.clear();
    for (int index : checkedIndices)
    {
        SelectedNotes.append(_filteredNotes[index]);
    }

    Copies = copies;
    Stride = stride;
    StartNumber = startNum;
    SearchKeyword = txtSearchKeyword->text();
    ReplacePattern = txtReplacePattern->text();

    // Save persisted state
    AppSettings::Current.BatchMultiply_Copies = Copies;
    AppSettings::Current.BatchMultiply_Stride = txtStride->text();
    AppSettings::Current.BatchMultiply_StartNum = StartNumber;
    AppSettings::Current.BatchMultiply_Search = SearchKeyword;
    AppSettings::Current.BatchMultiply_Replace = ReplacePattern;
    AppSettings::Save();

    accept();
}

void BatchNoteMultiplierDialog::onBtnCancel()
{
    reject();
}

} // namespace RALE
