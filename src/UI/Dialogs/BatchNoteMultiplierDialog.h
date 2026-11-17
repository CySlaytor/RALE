#pragma once

#include <QDialog>
#include <QListWidget>
#include <QLineEdit>
#include <QPushButton>
#include <QList>
#include "../../Models/LogicModels.h"

namespace RALogicEditor {

class BatchNoteMultiplierDialog : public QDialog
{
    Q_OBJECT

public:
    QList<CodeNote> SelectedNotes;
    int Copies = 1;
    int64_t Stride = 0;
    int StartNumber = 2;
    QString SearchKeyword = "";
    QString ReplacePattern = "";

    explicit BatchNoteMultiplierDialog(const QList<CodeNote> &allNotes, QWidget *parent = nullptr);

private slots:
    void onFilterChanged(const QString &filter);
    void onSelectAll();
    void onSelectNone();
    void onBtnOK();
    void onBtnCancel();

private:
    // Persisted state
    static int _lastCopies;
    static QString _lastStride;
    static int _lastStartNum;
    static QString _lastSearch;
    static QString _lastReplace;

    QList<CodeNote> _allNotes;
    QList<CodeNote> _filteredNotes;

    QLineEdit *txtFilter = nullptr;
    QPushButton *btnSelectAll = nullptr;
    QPushButton *btnSelectNone = nullptr;
    QListWidget *clbNotes = nullptr;

    QLineEdit *txtCopies = nullptr;
    QLineEdit *txtStride = nullptr;
    QLineEdit *txtStartNum = nullptr;
    QLineEdit *txtSearchKeyword = nullptr;
    QLineEdit *txtReplacePattern = nullptr;

    QPushButton *btnOK = nullptr;
    QPushButton *btnCancel = nullptr;

    void InitializeUI();
    void PopulateList(const QString &filter);
};

} // namespace RALogicEditor
