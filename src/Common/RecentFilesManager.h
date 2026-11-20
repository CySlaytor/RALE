#pragma once

#include <QList>
#include <QString>

namespace RALE {

class RecentFileEntry {
  public:
    QString Path = "";
    QString GameTitle = "";
};

class RecentFilesManager {
  public:
    QList<RecentFileEntry> Files;

    void Load();
    void Add(const QString& path);

  private:
    static const int MaxFiles = 10;

    void Save();
    QString ExtractGameTitle(const QString& path);

    // Save to JSON in Metadata folder to store Title + Path structure
    QString GetFilePathJson() const;
    // Legacy support
    QString GetFilePathTxt() const;
};

} // namespace RALE
