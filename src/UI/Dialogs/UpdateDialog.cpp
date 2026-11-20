#include "UpdateDialog.h"

#include "../../Common/AppSettings.h"

#include <QDesktopServices>
#include <QHBoxLayout>
#include <QUrl>
#include <QVBoxLayout>

#include <algorithm>

namespace RALE {

UpdateDialog::UpdateDialog(const QString& currentVer, const QString& latestVer, const QString& releaseNotes, const QString& downloadUrl,
                           QWidget* parent)
    : QDialog(parent), _downloadUrl(downloadUrl) {
    setWindowTitle("RetroAchievements Logic Editor - Software Update");
    setModal(true);
    setWindowFlags(windowFlags() & ~Qt::WindowContextHelpButtonHint);
    resize(540, 380);
    setMinimumSize(460, 300);

    setupLayout(currentVer, latestVer, releaseNotes);
    loadPositionSettings();
}

UpdateDialog::~UpdateDialog() = default;

void UpdateDialog::loadPositionSettings() {
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("UpdateDialog", p, s)) {
        move(p);
        if (s.isValid()) {
            resize(std::max(s.width(), 460), std::max(s.height(), 300));
        }
    }
}

void UpdateDialog::savePositionSettings() { AppSettings::SaveDialogGeometry("UpdateDialog", pos(), size()); }

void UpdateDialog::done(int r) {
    savePositionSettings();
    QDialog::done(r);
}

void UpdateDialog::setupLayout(const QString& currentVer, const QString& latestVer, const QString& releaseNotes) {
    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(12, 12, 12, 12);
    mainLayout->setSpacing(10);

    _statusLabel = new QLabel("A new version of RetroAchievements Logic Editor is available!", this);
    QFont statusFont = _statusLabel->font();
    statusFont.setPointSize(11);
    statusFont.setBold(true);
    _statusLabel->setFont(statusFont);
    mainLayout->addWidget(_statusLabel);

    _versionLabel = new QLabel(QString("Current Version: %1  |  Latest Version: %2").arg(currentVer, latestVer), this);
    mainLayout->addWidget(_versionLabel);

    mainLayout->addWidget(new QLabel("Release Notes:", this));

    _notesBrowser = new QTextBrowser(this);
    _notesBrowser->setPlainText(releaseNotes);
    _notesBrowser->setFrameShape(QFrame::StyledPanel);
    mainLayout->addWidget(_notesBrowser, 1);

    auto* buttonLayout = new QHBoxLayout();
    buttonLayout->setSpacing(8);

    _skipUpdateButton = new QPushButton("Skip This Update", this);
    _skipUpdateButton->setMinimumWidth(115);
    _skipUpdateButton->setFixedHeight(26);
    _skipUpdateButton->setAutoDefault(false);
    _skipUpdateButton->setDefault(false);

    _remindLaterButton = new QPushButton("Remind Me Later", this);
    _remindLaterButton->setMinimumWidth(115);
    _remindLaterButton->setFixedHeight(26);
    _remindLaterButton->setAutoDefault(false);
    _remindLaterButton->setDefault(false);

    _updateNowButton = new QPushButton("Update Now", this);
    _updateNowButton->setObjectName("updateNowButton");
    _updateNowButton->setMinimumWidth(105);
    _updateNowButton->setFixedHeight(26);
    _updateNowButton->setDefault(true);

    buttonLayout->addWidget(_skipUpdateButton);
    buttonLayout->addStretch();
    buttonLayout->addWidget(_remindLaterButton);
    buttonLayout->addWidget(_updateNowButton);
    mainLayout->addLayout(buttonLayout);

    connect(_skipUpdateButton, &QPushButton::clicked, this, &UpdateDialog::onSkipUpdateClicked);
    connect(_remindLaterButton, &QPushButton::clicked, this, &UpdateDialog::onRemindLaterClicked);
    connect(_updateNowButton, &QPushButton::clicked, this, &UpdateDialog::onUpdateNowClicked);
}

void UpdateDialog::onUpdateNowClicked() {
    _selectedAction = Action::UpdateNow;
    QDesktopServices::openUrl(QUrl(_downloadUrl));
    accept();
}

void UpdateDialog::onRemindLaterClicked() {
    _selectedAction = Action::RemindLater;
    reject();
}

void UpdateDialog::onSkipUpdateClicked() {
    _selectedAction = Action::SkipUpdate;
    reject();
}

} // namespace RALE
