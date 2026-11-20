#include "Common/AppSettings.h"
#include "UI/MainWindow.h"
#include "UI/Styles/ThemeManager.h"

#include <QApplication>
#include <QFileInfo>
#include <QIcon>

int main(int argc, char* argv[]) {
    QApplication app(argc, argv);
    app.setApplicationName("RALE");
    app.setOrganizationName("RetroAchievements");
    app.setWindowIcon(QIcon(":/appicon.ico"));

#ifdef VERSION_STRING
    app.setApplicationVersion(VERSION_STRING);
#else
    app.setApplicationVersion("2.0.0");
#endif

    // Pre-load application configuration
    RALE::AppSettings::Load();

    // Automatic QComboBox popup configuration across the entire app
    app.installEventFilter(new RALE::GlobalComboBoxConfigurator(&app));

    // Disable default Qt tooltip slide/fade effects
    app.setEffectEnabled(Qt::UI_AnimateTooltip, false);
    app.setEffectEnabled(Qt::UI_FadeTooltip, false);

    // Symmetric tooltip fade-in / fade-out animation filter
    app.installEventFilter(new RALE::TooltipAnimationFilter(&app));

    // Apply user selected theme from settings (defaults to Light)
    auto activeTheme = (RALE::AppSettings::Current.Theme.compare("Dark", Qt::CaseInsensitive) == 0) ? RALE::ThemeManager::Theme::Dark
                                                                                                    : RALE::ThemeManager::Theme::Light;

    RALE::ThemeManager::ApplyTheme(activeTheme);

    RALE::MainWindow mainWindow;
    mainWindow.show();

    // Use QCoreApplication::arguments to ensure Unicode command-line paths are parsed reliably.
    // Ignore Qt/platform switches and only open an argument that is an existing file.
    const QStringList args = QCoreApplication::arguments();
    for (int i = 1; i < args.size(); ++i) {
        if (args.at(i).startsWith('-'))
            continue;
        if (!QFileInfo::exists(args.at(i)))
            continue;
        mainWindow.LoadFiles(args.at(i));
        break;
    }

    return app.exec();
}
