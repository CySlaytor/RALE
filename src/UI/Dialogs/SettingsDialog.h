#pragma once

#include <QCheckBox>
#include <QComboBox>
#include <QDialog>
#include <QLineEdit>
#include <QPlainTextEdit>
#include <QPushButton>
#include <QTabWidget>

namespace RALE {

// Dedicated Settings dialog providing Appearance and Code Note configuration matching Pointer Finder 2.0
class SettingsDialog : public QDialog {
    Q_OBJECT
  public:
    explicit SettingsDialog(QWidget* parent = nullptr);
    ~SettingsDialog() override = default;

  protected:
    void done(int r) override;

  private slots:
    void onThemeChanged(const QString& themeName);
    void onCodeNoteSettingChanged();
    void onCodeNoteHelpClicked();
    void onResetAllClicked();

  private:
    QTabWidget* m_tabWidget = nullptr;

    // Appearance Tab
    QComboBox* m_themeCombo = nullptr;
    QPushButton* m_resetAllButton = nullptr;

    // Code Notes Tab
    QLineEdit* m_prefixEdit = nullptr;
    QLineEdit* m_suffixEdit = nullptr;
    QCheckBox* m_alignCheck = nullptr;
    QCheckBox* m_suffixLastLineCheck = nullptr;
    QCheckBox* m_autoPopulatePointersCheck = nullptr;
    QPlainTextEdit* m_previewEdit = nullptr;
    QPushButton* m_codeNoteHelpBtn = nullptr;

    QPushButton* m_closeButton = nullptr;
    bool m_isInitializing = true;
    bool m_isResetting = false;

    void setupLayout();
    void setupGeneralTab();
    void setupCodeNotesTab();
    void updateCodeNotePreview();
    void loadPositionSettings();
    void savePositionSettings();
};

} // namespace RALE
