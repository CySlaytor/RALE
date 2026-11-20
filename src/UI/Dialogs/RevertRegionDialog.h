#pragma once

#include <QComboBox>
#include <QDialog>
#include <QPushButton>
#include <QStringList>

namespace RALE {

class RevertRegionDialog : public QDialog {
    Q_OBJECT

  public:
    QString SelectedRegion = "";

    explicit RevertRegionDialog(const QStringList& detectedRegions, QWidget* parent = nullptr);

  protected:
    void done(int r) override;

  private slots:
    void onBtnOK();

  private:
    QComboBox* cmbRegions = nullptr;
    QPushButton* btnOK = nullptr;

    void InitializeUI();
    void loadPositionSettings();
    void savePositionSettings();
};

} // namespace RALE
