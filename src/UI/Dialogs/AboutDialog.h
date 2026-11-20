#pragma once

#include <QDialog>
#include <QLabel>
#include <QPushButton>

namespace RALE {

// This dialog shows general information about the application, its version number,
// purpose, and identifies the creator and project contributors.
class AboutDialog : public QDialog {
    Q_OBJECT
  public:
    explicit AboutDialog(QWidget* parent = nullptr);
    ~AboutDialog() override;

  protected:
    void done(int r) override;

  private:
    QLabel* m_iconLabel = nullptr;
    QLabel* m_titleLabel = nullptr;
    QLabel* m_descLabel = nullptr;
    QLabel* m_creditsLabel = nullptr;
    QPushButton* m_closeButton = nullptr;

    void setupLayout();
    void loadPositionSettings();
    void savePositionSettings();
};

} // namespace RALE
