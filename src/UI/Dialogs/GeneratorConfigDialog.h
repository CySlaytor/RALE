#pragma once

#include "../../Models/GeneratorModels.h"

#include <QCheckBox>
#include <QDialog>
#include <QLineEdit>
#include <QPushButton>
#include <QSet>
#include <QStringList>
#include <QTreeWidget>

#include <memory>

namespace RALE {

// A dialog to capture Copies, Stride, and select multiple Addresses for the Logic Generator
class GeneratorConfigDialog : public QDialog {
    Q_OBJECT

  public:
    // ViewModel to handle hierarchy and display formatting
    class GeneratorItemViewModel {
      public:
        QString Address = "";
        QString Description = "";
        QList<std::shared_ptr<GeneratorItemViewModel>> Children;

        QString ToString() const;
    };

    int Copies = 1;
    int Stride = 0;
    bool IsArray = false;
    bool DuplicateSelectedOnly = false;

    // List of addresses the user selected to apply the generator rule to
    QStringList SelectedAddresses;

    // Constructor now accepts a hierarchical list of items and pre-selected addresses
    GeneratorConfigDialog(const QList<std::shared_ptr<GeneratorItemViewModel>>& rootItems, int defaultStride = 0,
                          const GeneratorDefinition* existingDef = nullptr, const QSet<QString>* preSelectedAddresses = nullptr,
                          QWidget* parent = nullptr);

  protected:
    void done(int r) override;

  private slots:
    void onTreeItemChanged(QTreeWidgetItem* item, int column);
    void onSelectAll();
    void onBtnOK();
    void onBtnCancel();

  private:
    // Static field to track if user explicitly customized stride this session
    static bool _userModifiedStride; // Tracks if the user has explicitly set a stride this session

    QTreeWidget* tvAddresses = nullptr;
    QLineEdit* txtCopies = nullptr;
    QLineEdit* txtStride = nullptr;
    QCheckBox* chkGenerateAsArray = nullptr;
    QCheckBox* chkDuplicateSelectedOnly = nullptr;
    QPushButton* btnSelectAll = nullptr;
    QPushButton* btnOK = nullptr;
    QPushButton* btnCancel = nullptr;

    bool _isUpdatingCheck = false;

    void InitializeUI();
    void loadPositionSettings();
    void savePositionSettings();
    void InitializeTreeView(const QList<std::shared_ptr<GeneratorItemViewModel>>& items);
    QTreeWidgetItem* CreateTreeNode(const GeneratorItemViewModel& item);
    void CheckNodesRecursive(QTreeWidgetItem* item, const QSet<QString>& addressesToCheck);
    void SetChildrenChecked(QTreeWidgetItem* item, bool isChecked);
    void CollectCheckedAddresses(QTreeWidgetItem* item);
    bool IsNodeTreeChecked(QTreeWidgetItem* item) const;
};

} // namespace RALE
