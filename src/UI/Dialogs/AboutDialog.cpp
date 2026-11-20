#include "AboutDialog.h"

#include "../../Common/AppSettings.h"
#include "../Styles/ThemeManager.h"

#include <QFrame>
#include <QHBoxLayout>
#include <QVBoxLayout>

#ifndef VERSION_STRING
#define VERSION_STRING "2.0.0"
#endif

namespace RALE {

AboutDialog::AboutDialog(QWidget* parent) : QDialog(parent) {
    setWindowTitle("About RetroAchievements Logic Editor");
    setModal(true);
    setFixedSize(520, 430);
    setWindowFlags(windowFlags() & ~Qt::WindowContextHelpButtonHint);

    setupLayout();
    loadPositionSettings();
}

AboutDialog::~AboutDialog() = default;

void AboutDialog::loadPositionSettings() {
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("AboutDialog", p, s)) {
        move(p);
    }
}

void AboutDialog::savePositionSettings() { AppSettings::SaveDialogGeometry("AboutDialog", pos()); }

void AboutDialog::done(int r) {
    savePositionSettings();
    QDialog::done(r);
}

void AboutDialog::setupLayout() {
    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(15, 15, 15, 15);
    mainLayout->setSpacing(12);

    auto* topLayout = new QHBoxLayout();
    topLayout->setSpacing(15);

    m_iconLabel = new QLabel(this);
    QIcon appIcon(":/appicon.ico");
    if (!appIcon.isNull()) {
        m_iconLabel->setPixmap(appIcon.pixmap(QSize(64, 64)));
    }
    m_iconLabel->setAlignment(Qt::AlignTop);
    topLayout->addWidget(m_iconLabel);

    auto* titleContainer = new QVBoxLayout();
    titleContainer->setSpacing(2);

    m_titleLabel = new QLabel("RetroAchievements Logic Editor", this);
    QFont titleFont = m_titleLabel->font();
    titleFont.setPointSize(14);
    titleFont.setBold(true);
    m_titleLabel->setFont(titleFont);
    titleContainer->addWidget(m_titleLabel);

    QLabel* versionLabel = new QLabel(QString("Version %1").arg(VERSION_STRING), this);
    QFont verFont = versionLabel->font();
    verFont.setBold(true);
    versionLabel->setFont(verFont);
    titleContainer->addWidget(versionLabel);

    titleContainer->addStretch();
    topLayout->addLayout(titleContainer, 1);
    mainLayout->addLayout(topLayout);

    m_descLabel = new QLabel(this);
    m_descLabel->setWordWrap(true);
    m_descLabel->setTextFormat(Qt::RichText);
    m_descLabel->setText(
        "<p style='line-height: 125%;'><b>RALE</b> is an independent achievement development and memory note editing "
        "utility built to offer a more streamlined, feature-rich authoring experience. Its design brings together the "
        "workflow improvements and capabilities we wish were available natively in the RetroAchievements toolkit.</p>"
        "<p style='line-height: 125%;'>Originally developed in C# as <b>RALogicEditor</b>, the project was rebranded as "
        "<b>RALE</b> and ported to C++ and Qt. Since then, it has grown well beyond its original implementation with a "
        "renewed architecture, a refreshed interface, and deeper tooling for logic validation, hierarchy management, and "
        "multi-region development.</p>");
    mainLayout->addWidget(m_descLabel);

    auto* divider = new QFrame(this);
    divider->setFrameShape(QFrame::HLine);
    divider->setFrameShadow(QFrame::Sunken);
    mainLayout->addWidget(divider);

    m_creditsLabel = new QLabel(this);
    m_creditsLabel->setWordWrap(true);
    m_creditsLabel->setTextFormat(Qt::RichText);
    m_creditsLabel->setOpenExternalLinks(true);
    m_creditsLabel->setTextInteractionFlags(Qt::TextBrowserInteraction);
    m_creditsLabel->setText(
        "<b>Project Contributors:</b>"
        "<table cellspacing='4' cellpadding='0' style='margin-top: 5px;'>"
        "<tr>"
        "<td valign='top' style='width: 80px;'><b>Creator:</b></td>"
        "<td valign='top'><a href='https://retroachievements.org/user/CySlaytor'>CySlaytor</a> &mdash; Concept, architecture, "
        "and ongoing development of RALE.</td>"
        "</tr>"
        "<tr>"
        "<td valign='top'><b>Artwork:</b></td>"
        "<td valign='top'><a href='https://retroachievements.org/user/ZeeRA'>ZeeRA</a> &mdash; Original design of the application's "
        "graphic icon (the LE logo).</td>"
        "</tr>"
        "</table>");
    mainLayout->addWidget(m_creditsLabel, 1);

    auto* buttonLayout = new QHBoxLayout();
    m_closeButton = new QPushButton("Close", this);
    m_closeButton->setObjectName("closeButton");
    m_closeButton->setFixedWidth(75);
    buttonLayout->addStretch();
    buttonLayout->addWidget(m_closeButton);
    mainLayout->addLayout(buttonLayout);

    connect(m_closeButton, &QPushButton::clicked, this, &QDialog::accept);
}

} // namespace RALE
