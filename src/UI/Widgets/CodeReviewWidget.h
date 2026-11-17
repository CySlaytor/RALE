#pragma once

#include <QWidget>
#include <QTreeWidget>
#include <QComboBox>
#include <QLabel>
#include <QList>
#include "../../Models/ValidationModels.h"

namespace RALogicEditor {

class CodeReviewWidget : public QWidget
{
    Q_OBJECT

public:
    explicit CodeReviewWidget(QWidget *parent = nullptr);

    void SetIssues(const QList<ValidationIssue> &issues);

signals:
    void IssueSelected(const ValidationIssue &issue);

private slots:
    void onFilterChanged();
    void onItemDoubleClicked(QTreeWidgetItem *item, int column);
    void onItemActivated(QTreeWidgetItem *item, int column);

private:
    QComboBox *cmbFilter = nullptr;
    QLabel *lblStatus = nullptr;
    QLabel *lblHeader = nullptr;
    QTreeWidget *listIssues = nullptr;

    QList<ValidationIssue> _allIssues;
    QList<ValidationIssue> _filteredIssues;

    void InitializeUI();
    void ApplyFilter();
    void UpdateStatus(int count);
    QString GetSeverityIcon(FeedbackSeverity severity) const;
    void TriggerIssueNavigation();
};

} // namespace RALogicEditor
