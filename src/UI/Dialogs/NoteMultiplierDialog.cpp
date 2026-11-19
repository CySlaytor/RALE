#include "NoteMultiplierDialog.h"
#include "../Styles/ThemeManager.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QFormLayout>
#include <QMessageBox>

namespace RALE {

int NoteMultiplierDialog::_lastCopies = 1;
QString NoteMultiplierDialog::_lastStride = "0x0";
int NoteMultiplierDialog::_lastStartNum = 2;
int NoteMultiplierDialog::_lastNamingIdx = 1;
QString NoteMultiplierDialog::_lastSearch = "";
QString NoteMultiplierDialog::_lastReplace = "";
int NoteMultiplierDialog::_lastTargetIdx = 0;

NoteMultiplierDialog::NoteMultiplierDialog(QWidget *parent)
    : QDialog(parent)
{
    InitializeUI();

    // Load persisted state
    txtCopies->setText(QString::number(_lastCopies));
    txtStride->setText(_lastStride);
    txtStartNum->setText(QString::number(_lastStartNum));
    txtSearch->setText(_lastSearch);
    txtReplace->setText(_lastReplace);
    cmbNaming->setCurrentIndex(_lastNamingIdx);
    cmbStrideTarget->setCurrentIndex(_lastTargetIdx);

    this->setWindowTitle("Multiply Selected Text");
}

void NoteMultiplierDialog::InitializeUI()
{
    setFixedSize(420, 330);

    auto *mainLayout = new QVBoxLayout(this);
    auto *formLayout = new QFormLayout();

    txtCopies = new QLineEdit("1", this);
    formLayout->addRow("Number of Copies:", txtCopies);

    txtStride = new QLineEdit("0x0", this);
    formLayout->addRow("Stride (Address):", txtStride);

    txtStartNum = new QLineEdit("2", this);
    formLayout->addRow("Starting Number:", txtStartNum);

    cmbNaming = new QComboBox(this);
    ThemeManager::SetupComboBox(cmbNaming, 14);
    cmbNaming->addItems({
        "Append \" (Slot N)\"",
        "Replace \"Slot 1\" -> \"Slot N\"",
        "Replace \"Player 1\" -> \"Player N\"",
        "Custom Pattern..."
    });
    connect(cmbNaming, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &NoteMultiplierDialog::onNamingIndexChanged);
    formLayout->addRow("Naming Style:", cmbNaming);

    mainLayout->addLayout(formLayout);

    // Custom panel
    pnlCustom = new QWidget(this);
    auto *customLayout = new QFormLayout(pnlCustom);
    customLayout->setContentsMargins(0, 0, 0, 0);

    txtSearch = new QLineEdit(pnlCustom);
    customLayout->addRow("Text to Find:", txtSearch);

    txtReplace = new QLineEdit(pnlCustom);
    customLayout->addRow("Replace Name:", txtReplace);

    auto *lblHelp = new QLabel("Tip: Use {N} for the target slot/player number.", pnlCustom);
    lblHelp->setStyleSheet("color: gray;");
    customLayout->addRow("", lblHelp);

    pnlCustom->setVisible(false);
    mainLayout->addWidget(pnlCustom);

    auto *targetLayout = new QFormLayout();
    cmbStrideTarget = new QComboBox(this);
    ThemeManager::SetupComboBox(cmbStrideTarget, 14);
    cmbStrideTarget->addItems({
        "All Offsets (Array of Structs)",
        "Parent Offsets Only (Array of Pointers)",
        "Child Offsets Only (Array inside a Struct)"
    });
    targetLayout->addRow("Apply Stride To:", cmbStrideTarget);
    mainLayout->addLayout(targetLayout);

    mainLayout->addStretch();

    auto *btnLayout = new QHBoxLayout();
    btnLayout->addStretch();
    btnOK = new QPushButton("OK", this);
    connect(btnOK, &QPushButton::clicked, this, &NoteMultiplierDialog::onBtnOK);
    btnCancel = new QPushButton("Cancel", this);
    connect(btnCancel, &QPushButton::clicked, this, &NoteMultiplierDialog::onBtnCancel);
    btnLayout->addWidget(btnOK);
    btnLayout->addWidget(btnCancel);
    mainLayout->addLayout(btnLayout);
}

void NoteMultiplierDialog::onNamingIndexChanged(int index)
{
    // Show custom text boxes only if Custom pattern is selected
    pnlCustom->setVisible(index == 3);
}

void NoteMultiplierDialog::onBtnOK()
{
    bool ok = false;
    int copies = txtCopies->text().toInt(&ok);
    if (!ok || copies <= 0)
    {
        QMessageBox::warning(this, "Input Error", "Please enter a valid number of copies (1 or more).");
        return;
    }

    QString strideText = txtStride->text().trimmed();
    bool isHex = strideText.startsWith("0x", Qt::CaseInsensitive);
    if (isHex) strideText = strideText.mid(2);

    int64_t stride = isHex ? strideText.toLongLong(&ok, 16) : strideText.toLongLong(&ok, 10);
    if (!ok)
    {
        QMessageBox::warning(this, "Input Error", "Please enter a valid stride (Integers or 0xHex).");
        return;
    }

    int startNum = txtStartNum->text().toInt(&ok);
    if (!ok)
    {
        QMessageBox::warning(this, "Input Error", "Please enter a valid Starting Number.");
        return;
    }

    Copies = copies;
    Stride = stride;
    Naming = static_cast<NamingStyle>(cmbNaming->currentIndex());
    SearchText = txtSearch->text();
    ReplaceText = txtReplace->text();
    StartNumber = startNum;
    TargetMode = static_cast<StrideTarget>(cmbStrideTarget->currentIndex());

    // Save state for next use
    _lastCopies = Copies;
    _lastStride = txtStride->text().trimmed();
    _lastStartNum = StartNumber;
    _lastNamingIdx = cmbNaming->currentIndex();
    _lastSearch = SearchText;
    _lastReplace = ReplaceText;
    _lastTargetIdx = cmbStrideTarget->currentIndex();

    accept();
}

void NoteMultiplierDialog::onBtnCancel()
{
    reject();
}

} // namespace RALE
