#pragma once

#include <QDialog>
#include <QComboBox>
#include <QPushButton>
#include <QStringList>

namespace RALogicEditor {

class RevertRegionDialog : public QDialog
{
    Q_OBJECT

public:
    QString SelectedRegion = "";

    explicit RevertRegionDialog(const QStringList &detectedRegions, QWidget *parent = nullptr);

private slots:
    void onBtnOK();

private:
    QComboBox *cmbRegions = nullptr;
    QPushButton *btnOK = nullptr;

    void InitializeUI();
};

} // namespace RALogicEditor
