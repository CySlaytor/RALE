#pragma once

#include "../../Models/LogicModels.h"

#include <QDialog>
#include <QLineEdit>
#include <QList>
#include <QListWidget>
#include <QPushButton>

namespace RALE {

class BatchNoteMultiplierDialog : public QDialog {
    Q_OBJECT

  public:
    QList<CodeNote> SelectedNotes;
    int Copies = 1;
    int64_t Stride = 0;
    int StartNumber = 2;
    QString SearchKeyword = "";
    QString ReplacePattern = "";

    explicit BatchNoteMultiplierDialog(const QList<CodeNote>& allNotes, QWidget* parent = nullptr);

  protected:
    void done(int r) override;

  private slots:
    void onFilterChanged(const QString& filter);
    void onSelectAll();
    void onSelectNone();
    void onBtnOK();
    void onBtnCancel();

  private:
    QList<CodeNote> _allNotes;
    QList<CodeNote> _filteredNotes;

    QLineEdit* txtFilter = nullptr;
    QPushButton* btnSelectAll = nullptr;
    QPushButton* btnSelectNone = nullptr;
    QListWidget* clbNotes = nullptr;

    QLineEdit* txtCopies = nullptr;
    QLineEdit* txtStride = nullptr;
    QLineEdit* txtStartNum = nullptr;
    QLineEdit* txtSearchKeyword = nullptr;
    QLineEdit* txtReplacePattern = nullptr;

    QPushButton* btnOK = nullptr;
    QPushButton* btnCancel = nullptr;

    void InitializeUI();
    void loadPositionSettings();
    void savePositionSettings();
    void PopulateList(const QString& filter);
};

} // namespace RALE
