#pragma once

#include <QDialog>
#include <QLineEdit>
#include <QComboBox>
#include <QWidget>
#include <QLabel>
#include <QPushButton>
#include "../../Common/CodeNoteMultiplier.h"

namespace RALogicEditor {

class NoteMultiplierDialog : public QDialog
{
    Q_OBJECT

public:
    explicit NoteMultiplierDialog(QWidget *parent = nullptr);

    int Copies = 1;
    int64_t Stride = 0;
    NamingStyle Naming = NamingStyle::ReplaceSlot1;
    QString SearchText = "";
    QString ReplaceText = "";
    int StartNumber = 2;
    StrideTarget TargetMode = StrideTarget::All;

private slots:
    void onNamingIndexChanged(int index);
    void onBtnOK();
    void onBtnCancel();

private:
    // Static fields to persist state across session
    static int _lastCopies;
    static QString _lastStride;
    static int _lastStartNum;
    static int _lastNamingIdx;
    static QString _lastSearch;
    static QString _lastReplace;
    static int _lastTargetIdx;

    QLineEdit *txtCopies = nullptr;
    QLineEdit *txtStride = nullptr;
    QLineEdit *txtStartNum = nullptr;
    QComboBox *cmbNaming = nullptr;
    QWidget *pnlCustom = nullptr;
    QLineEdit *txtSearch = nullptr;
    QLineEdit *txtReplace = nullptr;
    QComboBox *cmbStrideTarget = nullptr;
    QPushButton *btnOK = nullptr;
    QPushButton *btnCancel = nullptr;

    void InitializeUI();
};

} // namespace RALogicEditor
