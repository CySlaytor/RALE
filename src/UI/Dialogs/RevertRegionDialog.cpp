#include "RevertRegionDialog.h"
#include "../Styles/ThemeManager.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>

namespace RALE {

RevertRegionDialog::RevertRegionDialog(const QStringList &detectedRegions, QWidget *parent)
    : QDialog(parent)
{
    InitializeUI();
    cmbRegions->clear();
    for (const auto &r : detectedRegions)
        cmbRegions->addItem(r);
    if (cmbRegions->count() > 0) cmbRegions->setCurrentIndex(0);
}

void RevertRegionDialog::InitializeUI()
{
    setWindowTitle("Revert Multi-Region");
    setMinimumSize(320, 105);

    auto *mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(12, 12, 12, 12);
    mainLayout->setSpacing(8);
    mainLayout->addWidget(new QLabel("Select Region to Keep (Base):", this));

    auto *row = new QHBoxLayout();
    cmbRegions = new QComboBox(this);
    ThemeManager::SetupComboBox(cmbRegions, 14);
    row->addWidget(cmbRegions, 1);

    btnOK = new QPushButton("Revert", this);
    btnOK->setFixedSize(75, 23);
    connect(btnOK, &QPushButton::clicked, this, &RevertRegionDialog::onBtnOK);
    row->addWidget(btnOK);

    mainLayout->addLayout(row);
    adjustSize();
}

void RevertRegionDialog::onBtnOK()
{
    SelectedRegion = cmbRegions->currentText();
    accept();
}

} // namespace RALE
