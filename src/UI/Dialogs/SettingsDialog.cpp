#include "SettingsDialog.h"
#include "../../Common/AppSettings.h"
#include "../Styles/ThemeManager.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QFormLayout>
#include <QGroupBox>
#include <QLabel>
#include <QMessageBox>
#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QProcess>
#include <algorithm>

namespace RALE {

SettingsDialog::SettingsDialog(QWidget *parent)
    : QDialog(parent)
{
    setWindowTitle("Settings");
    setModal(true);
    resize(500, 360);

    setupLayout();

    m_isInitializing = true;

    m_themeCombo->setCurrentText(AppSettings::Current.Theme.compare("Dark", Qt::CaseInsensitive) == 0 ? "Dark" : "Light");

    m_prefixEdit->setText(AppSettings::Current.CodeNotePrefix);
    m_suffixEdit->setText(AppSettings::Current.CodeNoteSuffix);
    m_alignCheck->setChecked(AppSettings::Current.CodeNoteAlignSuffixes);
    m_suffixLastLineCheck->setChecked(AppSettings::Current.CodeNoteSuffixOnLastLineOnly);
    m_autoPopulatePointersCheck->setChecked(AppSettings::Current.CodeNoteAutoPopulatePointers);
    m_suffixLastLineCheck->setEnabled(!AppSettings::Current.CodeNoteAutoPopulatePointers);

    m_isInitializing = false;
    updateCodeNotePreview();
    loadPositionSettings();
}

void SettingsDialog::loadPositionSettings()
{
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("SettingsDialog", p, s))
    {
        move(p);
        if (s.isValid())
        {
            resize(std::max(s.width(), 450), std::max(s.height(), 320));
        }
    }
}

void SettingsDialog::savePositionSettings()
{
    if (m_isResetting) return;
    AppSettings::SaveDialogGeometry("SettingsDialog", pos(), size());
}

void SettingsDialog::done(int r)
{
    savePositionSettings();
    QDialog::done(r);
}

void SettingsDialog::setupLayout()
{
    auto *mainLayout = new QVBoxLayout(this);

    m_tabWidget = new QTabWidget(this);
    setupGeneralTab();
    setupCodeNotesTab();
    mainLayout->addWidget(m_tabWidget);

    m_closeButton = new QPushButton("Close", this);
    m_closeButton->setObjectName("closeButton");
    m_closeButton->setAutoDefault(false);
    m_closeButton->setDefault(false);
    mainLayout->addWidget(m_closeButton, 0, Qt::AlignRight);

    connect(m_closeButton, &QPushButton::clicked, this, &QDialog::accept);
}

void SettingsDialog::setupGeneralTab()
{
    auto *tab = new QWidget(this);
    auto *layout = new QVBoxLayout(tab);

    auto *themeGroup = new QGroupBox("Appearance", tab);
    auto *themeLayout = new QHBoxLayout(themeGroup);
    themeLayout->addWidget(new QLabel("Active Theme:", themeGroup));
    m_themeCombo = new QComboBox(themeGroup);
    ThemeManager::SetupComboBox(m_themeCombo, 8);
    m_themeCombo->addItems({ "Dark", "Light" });
    themeLayout->addWidget(m_themeCombo);
    themeLayout->addStretch();
    layout->addWidget(themeGroup);

    layout->addStretch();

    m_resetAllButton = new QPushButton("Reset All Settings to Default...", tab);
    m_resetAllButton->setObjectName("resetAllButton");
    m_resetAllButton->setAutoDefault(false);
    m_resetAllButton->setDefault(false);
    layout->addWidget(m_resetAllButton);

    m_tabWidget->addTab(tab, "General");

    connect(m_themeCombo, &QComboBox::currentTextChanged, this, &SettingsDialog::onThemeChanged);
    connect(m_resetAllButton, &QPushButton::clicked, this, &SettingsDialog::onResetAllClicked);
}

void SettingsDialog::setupCodeNotesTab()
{
    auto *tab = new QWidget(this);
    auto *tabLayout = new QVBoxLayout(tab);

    // Header row with QA standards help button
    auto *headerRow = new QHBoxLayout();
    auto *noteHeaderDesc = new QLabel("Configure code note indirection levels and formatting standards:", tab);
    m_codeNoteHelpBtn = new QPushButton("?", tab);
    m_codeNoteHelpBtn->setFixedSize(22, 22);
    m_codeNoteHelpBtn->setAutoDefault(false);
    m_codeNoteHelpBtn->setDefault(false);
    m_codeNoteHelpBtn->setToolTip("Why standard indentation matters (QA & Code Review standards)");

    headerRow->addWidget(noteHeaderDesc);
    headerRow->addStretch();
    headerRow->addWidget(m_codeNoteHelpBtn);
    tabLayout->addLayout(headerRow);

    auto *contentLayout = new QHBoxLayout();

    auto *formattingGroup = new QGroupBox("Formatting Options", tab);
    auto *formLayout = new QFormLayout(formattingGroup);
    m_prefixEdit = new QLineEdit(formattingGroup);
    m_suffixEdit = new QLineEdit(formattingGroup);
    m_alignCheck = new QCheckBox("Align Suffixes Vertically", formattingGroup);
    m_suffixLastLineCheck = new QCheckBox("Apply Suffix to Last Line Only", formattingGroup);
    m_autoPopulatePointersCheck = new QCheckBox("Auto-populate intermediate\npointer levels with template", formattingGroup);

    formLayout->addRow("Indentation Prefix:", m_prefixEdit);
    formLayout->addRow("Line Suffix:", m_suffixEdit);
    formLayout->addRow(m_alignCheck);
    formLayout->addRow(m_suffixLastLineCheck);
    formLayout->addRow(m_autoPopulatePointersCheck);
    contentLayout->addWidget(formattingGroup, 3);

    auto *previewGroup = new QGroupBox("Live Preview", tab);
    auto *previewLayout = new QVBoxLayout(previewGroup);
    m_previewEdit = new QPlainTextEdit(previewGroup);
    m_previewEdit->setReadOnly(true);
    m_previewEdit->setMinimumWidth(210);

    QFont monoFont("Consolas", 9);
    monoFont.setStyleHint(QFont::Monospace);
    m_previewEdit->setFont(monoFont);

    previewLayout->addWidget(m_previewEdit);
    contentLayout->addWidget(previewGroup, 2);

    tabLayout->addLayout(contentLayout, 1);
    m_tabWidget->addTab(tab, "Code Notes");

    connect(m_codeNoteHelpBtn, &QPushButton::clicked, this, &SettingsDialog::onCodeNoteHelpClicked);
    connect(m_prefixEdit, &QLineEdit::textChanged, this, &SettingsDialog::onCodeNoteSettingChanged);
    connect(m_suffixEdit, &QLineEdit::textChanged, this, &SettingsDialog::onCodeNoteSettingChanged);
    connect(m_alignCheck, &QCheckBox::checkStateChanged, this, &SettingsDialog::onCodeNoteSettingChanged);
    connect(m_suffixLastLineCheck, &QCheckBox::checkStateChanged, this, &SettingsDialog::onCodeNoteSettingChanged);
    connect(m_autoPopulatePointersCheck, &QCheckBox::checkStateChanged, this, &SettingsDialog::onCodeNoteSettingChanged);
}

void SettingsDialog::onThemeChanged(const QString &themeName)
{
    if (m_isInitializing) return;

    auto theme = (themeName.compare("Dark", Qt::CaseInsensitive) == 0)
        ? ThemeManager::Theme::Dark
        : ThemeManager::Theme::Light;

    ThemeManager::ApplyTheme(theme);
    AppSettings::Current.Theme = (theme == ThemeManager::Theme::Dark) ? "Dark" : "Light";
    AppSettings::Save();
}

void SettingsDialog::onCodeNoteSettingChanged()
{
    if (m_isInitializing) return;

    AppSettings::Current.CodeNotePrefix = m_prefixEdit->text();
    AppSettings::Current.CodeNoteSuffix = m_suffixEdit->text();
    AppSettings::Current.CodeNoteAlignSuffixes = m_alignCheck->isChecked();
    AppSettings::Current.CodeNoteSuffixOnLastLineOnly = m_suffixLastLineCheck->isChecked();
    AppSettings::Current.CodeNoteAutoPopulatePointers = m_autoPopulatePointersCheck->isChecked();

    m_suffixLastLineCheck->setEnabled(!m_autoPopulatePointersCheck->isChecked());

    AppSettings::Save();
    updateCodeNotePreview();
}

void SettingsDialog::updateCodeNotePreview()
{
    QString prefix = m_prefixEdit->text();
    if (prefix.isEmpty()) prefix = "+";
    QString suffix = m_suffixEdit->text();
    if (suffix.isEmpty()) suffix = "=";

    bool align = m_alignCheck->isChecked();
    bool suffixLastOnly = m_suffixLastLineCheck->isChecked() && m_suffixLastLineCheck->isEnabled();
    bool autoPopulate = m_autoPopulatePointersCheck->isChecked();

    QStringList lines;
    lines << QString("%1%2%3").arg("", "+", "0x04");
    lines << QString("%1%2%3").arg(prefix, "+", "0x2A0");
    lines << QString("%1%2%3").arg(prefix + prefix, "+", "0x1C");

    int maxLen = 0;
    if (align)
    {
        for (const auto &l : lines) maxLen = std::max(maxLen, static_cast<int>(l.length()));
    }

    QStringList formatted;
    for (int i = 0; i < lines.size(); ++i)
    {
        QString l = lines[i];
        QString s = (!suffixLastOnly || (i == lines.size() - 1)) ? suffix : "";
        QString pad = align ? QString(maxLen - l.length(), ' ') : "";
        formatted << QString("%1%2%3").arg(l, pad, s);
    }

    if (autoPopulate)
    {
        formatted[0].append("[32-bit] Pointer");
        formatted[1].append("[32-bit] Pointer");
    }
    formatted[2].append("[8-bit] Stage");

    QString preview = formatted.join("\n");
    QString dotIndent = "....";
    preview.append(QString("\n%1Bit0%2Stage 1").arg(dotIndent, suffix));
    preview.append(QString("\n%1Bit1%2Stage 2").arg(dotIndent, suffix));
    preview.append(QString("\n%1Bit2%2Stage 3").arg(dotIndent, suffix));

    QFont monoFont("Consolas", 9);
    monoFont.setStyleHint(QFont::Monospace);
    m_previewEdit->setFont(monoFont);
    m_previewEdit->setPlainText(preview);
}

void SettingsDialog::onCodeNoteHelpClicked()
{
    QDialog helpDialog(this);
    helpDialog.setWindowTitle("Recommended Code Note Structure");
    helpDialog.resize(620, 560);
    helpDialog.setWindowFlags(helpDialog.windowFlags() & ~Qt::WindowContextHelpButtonHint);

    auto *layout = new QVBoxLayout(&helpDialog);
    layout->setContentsMargins(14, 12, 14, 12);
    layout->setSpacing(6);

    auto *titleLabel = new QLabel("<b>Hierarchical Code Note Standards</b>", &helpDialog);
    titleLabel->setObjectName("settingsHelpTitleLabel");
    layout->addWidget(titleLabel);

    bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);
    QString codeBg = isDark ? "#2b2d31" : "#e3e5e8";
    QString codeFg = isDark ? "#e2e4e9" : "#2b2d31";
    QString codeBorder = isDark ? "#3d4149" : "#c4c7cc";

    auto codeBadge = [&](const QString &text) -> QString {
        return QString("<span style='background-color: %1; color: %2; border: 1px solid %3; font-family: Consolas, monospace; font-size: 11px;'>&nbsp;%4&nbsp;</span>")
            .arg(codeBg, codeFg, codeBorder, text.toHtmlEscaped());
    };

    auto *descLabel = new QLabel(
        QString(
            "<p style='line-height: 125%; margin: 0; padding: 0;'>RetroAchievements QA and Code Review (CR) teams encourage adopting "
            "standardized hierarchical code note formatting for all new sets and updates:</p>"
            "<ul style='margin-top: 4px; margin-bottom: 4px; margin-left: -28px; padding-left: 0; line-height: 130%;'>"
            "<li style='margin-bottom: 5px;'><b>Structure and Maintainability:</b> Consistent indentation and clear offset chains keep "
            "multi-level pointer notes transparent and easy to review, maintain, or update across achievement sets.</li>"
            "<li style='margin-bottom: 5px;'><b>Pointers (%1) vs. Enums (%2):</b> Plusses (%1) are now used exclusively for pointer indirection jumps, "
            "while dots (%2) are now used exclusively for value enumerations, establishing an immediate visual distinction between structural memory offsets and variable states.</li>"
            "<li style='margin-bottom: 5px;'><b>Indenting Enums:</b> Value enumerations now level up with dots matching the nesting depth of their parent variable "
            "(e.g., %3 directly beneath %4), keeping complex bitfield and state structures intuitive to read.</li>"
            "<li style='margin-bottom: 5px;'><b>Pointer Type Annotations:</b> Use %5 followed by an explicit pointer template such as %6 "
            "or %7 according to the platform architecture.</li>"
            "<li style='margin-bottom: 2px;'><b>Pipelines (%8) for Array Elements:</b> Pipelines are now reserved exclusively for documenting "
            "<b>array structures</b> and indexed element offsets (e.g., %9).</li>"
            "</ul>")
            .arg(codeBadge("+"), codeBadge("."), codeBadge("...0x00=Idle"), codeBadge("++0x28"))
            .arg(codeBadge("="), codeBadge("[32-bit] Pointer"), codeBadge("[24-bit] Pointer"))
            .arg(codeBadge("|"), codeBadge("+++|0x00")),
        &helpDialog);
    descLabel->setWordWrap(true);
    layout->addWidget(descLabel);

    layout->addWidget(new QLabel("<b>Exemplary Recommended Structure:</b>", &helpDialog));

    auto *exampleEdit = new QPlainTextEdit(&helpDialog);
    exampleEdit->setReadOnly(true);
    exampleEdit->setMinimumHeight(210);
    QFont monoFont("Consolas", 9);
    monoFont.setStyleHint(QFont::Monospace);
    exampleEdit->setFont(monoFont);
    exampleEdit->setPlainText("[32-bit] Pointer to PlayerObject\n\n"
                              "+0x00=[32-bit] Pointer to PlayerData\n"
                              "++0x14=[32-bit] Pointer to Coordinates\n"
                              "+++0x00=[Float] Position X\n"
                              "+++0x04=[Float] Position Y\n"
                              "+++0x08=[Float] Position Z\n"
                              "++0x28=[32-bit] Player State\n"
                              "...0x00=Idle\n"
                              "...0x01=Running\n"
                              "...0x02=Jumping\n"
                              "...0xFF=Dead\n"
                              "++0x30=[16-bit] Health\n"
                              "++0x40=[30x16 bytes] Pointer to PlayerInventory Array\n"
                              "+++|0x00=[32-bit] Weapon ID\n"
                              "....0x00=Pistol\n"
                              "....0x01=SMG\n"
                              "....0x02=Sniper Rifle");
    layout->addWidget(exampleEdit, 1);

    auto *closeBtn = new QPushButton("Close", &helpDialog);
    closeBtn->setObjectName("closeButton");
    closeBtn->setAutoDefault(false);
    closeBtn->setDefault(false);
    connect(closeBtn, &QPushButton::clicked, &helpDialog, &QDialog::accept);
    layout->addWidget(closeBtn, 0, Qt::AlignRight);

    helpDialog.exec();
}

void SettingsDialog::onResetAllClicked()
{
    auto result = QMessageBox::critical(this, "Confirm Reset All Settings",
                                        "This will reset ALL application settings, including window geometry, theme, and code note options.\n\n"
                                        "The settings.ini file will be deleted and the application will restart.\n\n"
                                        "Are you sure you want to proceed?",
                                        QMessageBox::Yes | QMessageBox::No, QMessageBox::No);

    if (result == QMessageBox::Yes)
    {
        m_isResetting = true;
        QString settingsFile = QDir(QCoreApplication::applicationDirPath()).filePath("settings.ini");
        if (QFile::exists(settingsFile))
        {
            QFile::remove(settingsFile);
        }

        QProcess::startDetached(QCoreApplication::applicationFilePath(), QStringList());
        QApplication::quit();
    }
}

} // namespace RALE
