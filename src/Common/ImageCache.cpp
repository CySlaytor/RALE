#include "ImageCache.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QNetworkReply>
#include <QPainter>

namespace RALE {

QMap<QString, QPixmap> ImageCache::_memoryCache;
QMap<QString, QList<ImageCache::CallbackEntry>> ImageCache::_pendingCallbacks;
QString ImageCache::LocalBadgeDirectory = "";
std::unique_ptr<QNetworkAccessManager> ImageCache::_networkManager = nullptr;
int ImageCache::_cacheGeneration = 0;

QString ImageCache::GetCacheDir() {
    // Saves to bin/Debug/Cache/Badges
    QString dir = QDir(QCoreApplication::applicationDirPath()).filePath("Cache/Badges");
    if (!QDir(dir).exists())
        QDir().mkpath(dir);
    return dir;
}

void ImageCache::Clear() {
    ++_cacheGeneration;
    _memoryCache.clear();
    _pendingCallbacks.clear();
    LocalBadgeDirectory = "";
}

/// Attempts to locate the "Badge" directory relative to the loaded file.
/// Standard RA Structure: ~RACache\Data\User.txt -> ~RACache\Badge\Image.png
void ImageCache::ConfigureLocalPaths(const QString& loadedFilePath) {
    LocalBadgeDirectory = "";
    if (loadedFilePath.isEmpty())
        return;

    try {
        QFileInfo fi(loadedFilePath);
        QDir dataDir = fi.dir();

        // 1. Check sibling "Badge" directory (Standard RA)
        // loadedFilePath is inside "Data", so we go up one level and look for "Badge"
        QDir parentDir = dataDir;
        if (parentDir.cdUp()) {
            QString siblingBadgeDir = parentDir.filePath("Badge");
            if (QDir(siblingBadgeDir).exists()) {
                LocalBadgeDirectory = siblingBadgeDir;
                return;
            }
        }

        // 2. Check subdirectory "Badge" (Non-standard / Flat)
        QString subBadgeDir = dataDir.filePath("Badge");
        if (QDir(subBadgeDir).exists()) {
            LocalBadgeDirectory = subBadgeDir;
        }
    } catch (...) { /* Ignore path errors */
    }
}

QPixmap ImageCache::GetBadge(const QString& badgeName, std::function<void(const QPixmap&)> callback, QObject* context) {
    if (badgeName.isEmpty() || badgeName == "00000")
        return CreatePlaceholder();

    // Normalize "local\hash.png" or "local/hash.png" to just "hash"
    QString cleanName = badgeName;

    // Strip directory info (local\)
    if (cleanName.contains('\\') || cleanName.contains('/')) {
        cleanName = QFileInfo(cleanName).fileName();
    }

    // Remove extension to normalize cache key
    // (RA Badges are typically accessed by hash, saved as .png)
    if (cleanName.endsWith(".png", Qt::CaseInsensitive)) {
        cleanName = QFileInfo(cleanName).completeBaseName();
    }

    // 1. Check Memory
    if (_memoryCache.contains(cleanName))
        return _memoryCache.value(cleanName);

    // 2. Check Local RA Directory (The "Clever" Lookup)
    // If we detected a Badge folder near the User.txt, check there first.
    if (!LocalBadgeDirectory.isEmpty()) {
        QString raLocalPath = QDir(LocalBadgeDirectory).filePath(QString("%1.png").arg(cleanName));
        if (QFile::exists(raLocalPath)) {
            QPixmap pix = LoadImageSafe(raLocalPath);
            if (!pix.isNull()) {
                _memoryCache.insert(cleanName, pix);
                return pix;
            }
        }
    }

    // 3. Check Internal App Cache
    QString appCachePath = QDir(GetCacheDir()).filePath(QString("%1.png").arg(cleanName));
    if (QFile::exists(appCachePath)) {
        QPixmap pix = LoadImageSafe(appCachePath);
        if (!pix.isNull()) {
            _memoryCache.insert(cleanName, pix);
            return pix;
        }
    }

    // 4. Download from RA
    if (_pendingCallbacks.contains(cleanName)) {
        if (callback)
            _pendingCallbacks[cleanName].append(CallbackEntry{context, context != nullptr, callback});
        return CreatePlaceholder();
    }

    if (callback)
        _pendingCallbacks[cleanName].append(CallbackEntry{context, context != nullptr, callback});

    if (!_networkManager) {
        _networkManager = std::make_unique<QNetworkAccessManager>();
        // Ensure network manager is cleaned up prior to QCoreApplication exit to prevent static destructor assertions
        if (qApp) {
            QObject::connect(qApp, &QCoreApplication::aboutToQuit, []() { _networkManager.reset(); });
        }
    }

    QUrl url(QString("https://media.retroachievements.org/Badge/%1.png").arg(cleanName));
    QNetworkRequest request(url);
    QNetworkReply* reply = _networkManager->get(request);
    int requestGen = _cacheGeneration;

    QObject::connect(reply, &QNetworkReply::finished, [reply, cleanName, appCachePath, requestGen]() {
        reply->deleteLater();
        QPixmap resultPixmap;

        if (reply->error() == QNetworkReply::NoError) {
            QByteArray data = reply->readAll();
            QFile file(appCachePath);
            if (file.open(QIODevice::WriteOnly)) {
                file.write(data);
                file.close();
            }

            QPixmap pix;
            if (pix.loadFromData(data)) {
                if (requestGen == _cacheGeneration)
                    _memoryCache.insert(cleanName, pix);
                resultPixmap = pix;
            }
        }

        if (requestGen != _cacheGeneration)
            return;

        if (resultPixmap.isNull())
            resultPixmap = CreatePlaceholder();

        auto callbacks = _pendingCallbacks.take(cleanName);
        for (const auto& entry : callbacks) {
            // Verify context object has not been destroyed before invoking callback
            if (entry.callback && (!entry.hasContext || !entry.context.isNull())) {
                entry.callback(resultPixmap);
            }
        }
    });

    return CreatePlaceholder();
}

QPixmap ImageCache::LoadImageSafe(const QString& path) {
    QPixmap pix;
    pix.load(path);
    return pix;
}

QPixmap ImageCache::CreatePlaceholder() {
    QPixmap pix(64, 64);
    pix.fill(Qt::lightGray);
    QPainter p(&pix);
    p.setPen(Qt::gray);
    p.drawRect(0, 0, 63, 63);
    p.drawText(pix.rect(), Qt::AlignCenter, "?");
    return pix;
}

} // namespace RALE
