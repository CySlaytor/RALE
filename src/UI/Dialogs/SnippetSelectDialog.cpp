#include "SnippetSelectDialog.h"
#include "../../Services/SnippetManager.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QMessageBox>

namespace RALE {

SnippetSelectDialog::SnippetSelectDialog(QWidget *parent)
    : QDialog(parent)
{
    InitializeUI();
    LoadSnippets();
}

void SnippetSelectDialog::InitializeUI()
{
    setWindowTitle("Snippet Library");
    setFixedSize(400, 280);

    auto *mainLayout = new QHBoxLayout(this);

    lstSnippets = new QListWidget(this);
    connect(lstSnippets, &QListWidget::currentRowChanged, this, &SnippetSelectDialog::onSelectionChanged);
    connect(lstSnippets, &QListWidget::itemDoubleClicked, this, &SnippetSelectDialog::onItemDoubleClicked);
    mainLayout->addWidget(lstSnippets, 1);

    auto *rightCol = new QVBoxLayout();

    btnInsert = new QPushButton("Insert", this);
    btnInsert->setEnabled(false);
    connect(btnInsert, &QPushButton::clicked, this, &SnippetSelectDialog::onBtnInsert);
    rightCol->addWidget(btnInsert);

    rightCol->addWidget(new QLabel("Preview:", this));

    txtPreview = new QPlainTextEdit(this);
    txtPreview->setReadOnly(true);
    rightCol->addWidget(txtPreview, 1);

    btnDelete = new QPushButton("Delete Snippet", this);
    btnDelete->setEnabled(false);
    connect(btnDelete, &QPushButton::clicked, this, &SnippetSelectDialog::onBtnDelete);
    rightCol->addWidget(btnDelete);

    mainLayout->addLayout(rightCol, 1);
}

void SnippetSelectDialog::LoadSnippets()
{
    lstSnippets->clear();
    auto snippets = SnippetManager::Load();
    for (const auto &snip : snippets)
    {
        auto *item = new QListWidgetItem(snip.Name, lstSnippets);
        item->setData(Qt::UserRole, snip.Logic);
    }
}

void SnippetSelectDialog::onSelectionChanged()
{
    auto *item = lstSnippets->currentItem();
    if (item != nullptr)
    {
        txtPreview->setPlainText(item->data(Qt::UserRole).toString());
        btnInsert->setEnabled(true);
        btnDelete->setEnabled(true);
    }
    else
    {
        txtPreview->clear();
        btnInsert->setEnabled(false);
        btnDelete->setEnabled(false);
    }
}

void SnippetSelectDialog::onBtnInsert()
{
    auto *item = lstSnippets->currentItem();
    if (item != nullptr)
    {
        Snippet s;
        s.Name = item->text();
        s.Logic = item->data(Qt::UserRole).toString();
        SelectedSnippet = s;
        accept();
    }
}

void SnippetSelectDialog::onBtnDelete()
{
    auto *item = lstSnippets->currentItem();
    if (item != nullptr)
    {
        if (QMessageBox::question(this, "Confirm", QString("Delete snippet '%1'?").arg(item->text())) == QMessageBox::Yes)
        {
            Snippet s;
            s.Name = item->text();
            s.Logic = item->data(Qt::UserRole).toString();
            SnippetManager::DeleteSnippet(s);
            LoadSnippets();
            txtPreview->clear();
        }
    }
}

void SnippetSelectDialog::onItemDoubleClicked(QListWidgetItem * /*item*/)
{
    onBtnInsert();
}

} // namespace RALE
