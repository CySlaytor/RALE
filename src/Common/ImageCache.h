#pragma once

#include <QString>
#include <QPixmap>
#include <QMap>
#include <QList>
#include <QNetworkAccessManager>
#include <QPointer>
#include <functional>

namespace RALE {

class ImageCache
{
public:
    static void Clear();

    /// Attempts to locate the "Badge" directory relative to the loaded file.
    /// Standard RA Structure: ~RACache\Data\User.txt -> ~RACache\Badge\Image.png
    static void ConfigureLocalPaths(const QString &loadedFilePath);

    static QPixmap GetBadge(const QString &badgeName, std::function<void(const QPixmap &)> callback = nullptr, QObject *context = nullptr);

    static QString LocalBadgeDirectory;

private:
    static QString GetCacheDir();
    static QPixmap CreatePlaceholder();
    static QPixmap LoadImageSafe(const QString &path);

    struct CallbackEntry {
        QPointer<QObject> context;
        bool hasContext = false;
        std::function<void(const QPixmap &)> callback;
    };

    static QMap<QString, QPixmap> _memoryCache;
    static QMap<QString, QList<CallbackEntry>> _pendingCallbacks;
    static std::unique_ptr<QNetworkAccessManager> _networkManager;
};

} // namespace RALE
