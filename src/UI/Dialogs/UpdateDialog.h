#pragma once

#include <QDialog>
#include <QLabel>
#include <QPushButton>
#include <QTextBrowser>

namespace RALE {

class UpdateDialog : public QDialog {
    Q_OBJECT

  public:
    enum class Action { None, RemindLater, SkipUpdate, UpdateNow };

    UpdateDialog(const QString& currentVer, const QString& latestVer, const QString& releaseNotes, const QString& downloadUrl,
                 QWidget* parent = nullptr);
    ~UpdateDialog() override;

    Action SelectedAction() const { return _selectedAction; }

  protected:
    void done(int r) override;

  private slots:
    void onUpdateNowClicked();
    void onRemindLaterClicked();
    void onSkipUpdateClicked();

  private:
    QString _downloadUrl;
    Action _selectedAction = Action::None;

    QLabel* _statusLabel = nullptr;
    QLabel* _versionLabel = nullptr;
    QTextBrowser* _notesBrowser = nullptr;
    QPushButton* _skipUpdateButton = nullptr;
    QPushButton* _remindLaterButton = nullptr;
    QPushButton* _updateNowButton = nullptr;

    void setupLayout(const QString& currentVer, const QString& latestVer, const QString& releaseNotes);
    void loadPositionSettings();
    void savePositionSettings();
};

} // namespace RALE
