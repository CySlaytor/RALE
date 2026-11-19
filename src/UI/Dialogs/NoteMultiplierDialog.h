#pragma once

#include <QDialog>
#include <QLineEdit>
#include <QComboBox>
#include <QWidget>
#include <QLabel>
#include <QPushButton>
#include "../../Common/CodeNoteMultiplier.h"

namespace RALE {

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

protected:
    void done(int r) override;

private slots:
    void onNamingIndexChanged(int index);
    void onBtnOK();
    void onBtnCancel();

private:
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
    void loadPositionSettings();
    void savePositionSettings();
};

} // namespace RALE
