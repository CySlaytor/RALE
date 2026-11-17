#pragma once

#include <QDialog>
#include <QListWidget>
#include <QPlainTextEdit>
#include <QPushButton>
#include <optional>
#include "../../Models/SnippetModels.h"

namespace RALogicEditor {

class SnippetSelectDialog : public QDialog
{
    Q_OBJECT

public:
    std::optional<Snippet> SelectedSnippet;

    explicit SnippetSelectDialog(QWidget *parent = nullptr);

private slots:
    void onSelectionChanged();
    void onBtnInsert();
    void onBtnDelete();
    void onItemDoubleClicked(QListWidgetItem *item);

private:
    QListWidget *lstSnippets = nullptr;
    QPushButton *btnInsert = nullptr;
    QPushButton *btnDelete = nullptr;
    QPlainTextEdit *txtPreview = nullptr;

    void InitializeUI();
    void LoadSnippets();
};

} // namespace RALogicEditor
