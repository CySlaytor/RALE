#include <QApplication>
#include <QFileInfo>
#include <QIcon>
#include <QStyleFactory>
#include <QPalette>
#include <QListView>
#include "Common/AppSettings.h"
#include "UI/MainWindow.h"
#include "UI/Styles/ThemeManager.h"

int main(int argc, char *argv[])
{
    QApplication app(argc, argv);
    app.setApplicationName("RALogicEditor");
    app.setOrganizationName("RetroAchievements");
    app.setWindowIcon(QIcon(":/appicon.ico"));

#ifdef VERSION_STRING
    app.setApplicationVersion(VERSION_STRING);
#else
    app.setApplicationVersion("2.0.0");
#endif

    // Pre-load application configuration
    RALogicEditor::AppSettings::Load();

    // Apply the Fusion style engine first to ensure predictable cross-platform QSS rendering
    app.setStyle(new RALogicEditor::ClassicDropdownStyle(QStyleFactory::create("Fusion")));

    // Automatic QComboBox popup configuration across the entire app
    app.installEventFilter(new RALogicEditor::GlobalComboBoxConfigurator(&app));

    // Disable default Qt tooltip slide/fade effects
    app.setEffectEnabled(Qt::UI_AnimateTooltip, false);
    app.setEffectEnabled(Qt::UI_FadeTooltip, false);

    // Symmetric tooltip fade-in / fade-out animation filter
    app.installEventFilter(new RALogicEditor::TooltipAnimationFilter(&app));

    // Initialize the style and dark palette on QApplication at startup (before creating widgets)
    QPalette darkPalette;
    darkPalette.setColor(QPalette::Window, QColor(32, 32, 32));
    darkPalette.setColor(QPalette::WindowText, Qt::white);
    darkPalette.setColor(QPalette::Base, QColor(25, 25, 25));
    darkPalette.setColor(QPalette::AlternateBase, QColor(32, 32, 32));
    darkPalette.setColor(QPalette::ToolTipBase, QColor(36, 36, 36));
    darkPalette.setColor(QPalette::ToolTipText, QColor(240, 240, 240));
    darkPalette.setColor(QPalette::Text, Qt::white);
    darkPalette.setColor(QPalette::Button, QColor(55, 55, 55));
    darkPalette.setColor(QPalette::ButtonText, Qt::white);
    darkPalette.setColor(QPalette::BrightText, Qt::red);
    darkPalette.setColor(QPalette::Link, QColor(3, 218, 198));
    darkPalette.setColor(QPalette::Highlight, QColor(58, 120, 203));
    darkPalette.setColor(QPalette::HighlightedText, Qt::white);
    darkPalette.setColor(QPalette::Disabled, QPalette::WindowText, QColor(128, 128, 128));
    darkPalette.setColor(QPalette::Disabled, QPalette::Text, QColor(128, 128, 128));
    darkPalette.setColor(QPalette::Disabled, QPalette::ButtonText, QColor(128, 128, 128));
    app.setPalette(darkPalette);

    // Apply user selected theme from settings (defaults to Light)
    auto activeTheme = (RALogicEditor::AppSettings::Current.Theme.compare("Dark", Qt::CaseInsensitive) == 0)
        ? RALogicEditor::ThemeManager::Theme::Dark
        : RALogicEditor::ThemeManager::Theme::Light;

    RALogicEditor::ThemeManager::ApplyTheme(activeTheme);

    RALogicEditor::MainWindow mainWindow;
    mainWindow.show();

    // Use QCoreApplication::arguments to ensure Unicode command-line paths are parsed reliably.
    // Ignore Qt/platform switches and only open an argument that is an existing file.
    const QStringList args = QCoreApplication::arguments();
    for (int i = 1; i < args.size(); ++i)
    {
        if (args.at(i).startsWith('-')) continue;
        if (!QFileInfo::exists(args.at(i))) continue;
        mainWindow.LoadFiles(args.at(i));
        break;
    }

    return app.exec();
}
