#pragma once

#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QObject>
#include <QPointer>
#include <QString>

namespace RALE {

// Queries the public GitHub repository API to verify if a new release is available.
class UpdateChecker : public QObject {
    Q_OBJECT

  public:
    explicit UpdateChecker(QObject* parent = nullptr);
    ~UpdateChecker() override;

    void checkForUpdates(bool silentOnLatest = false);
    void cancelManualCheck();

  signals:
    void updateCheckFinished(bool updateAvailable, const QString& latestVersion, const QString& releaseNotes, const QString& downloadUrl,
                             bool silentOnLatest);
    void updateCheckFailed(const QString& errorMessage, bool silentOnLatest);

  private slots:
    void onReplyFinished(QNetworkReply* reply);

  private:
    QNetworkAccessManager* _networkManager = nullptr;
    QPointer<QNetworkReply> _manualReply;

    static bool isVersionNewer(const QString& current, const QString& latest);
};

} // namespace RALE
