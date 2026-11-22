#include "CodeReviewWidget.h"

#include "../../Models/AssetModels.h"
#include "../Styles/ThemeManager.h"

#include <QDesktopServices>
#include <QEvent>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QKeyEvent>
#include <QMouseEvent>
#include <QSet>
#include <QUrl>
#include <QVBoxLayout>

#include <algorithm>

namespace RALE {

CodeReviewWidget::CodeReviewWidget(QWidget* parent) : QWidget(parent) {
    setMinimumHeight(60);
    InitializeUI();

    cmbFilter->addItem("Show All Issues");
    cmbFilter->addItem("Logic Warnings & Errors");
    cmbFilter->addItem("Missing Code Notes");
    cmbFilter->addItem("Code Notes");
    cmbFilter->addItem("Presentation & Writing");
    cmbFilter->addItem("Set Design");
    cmbFilter->setCurrentIndex(0);

    connect(cmbFilter, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &CodeReviewWidget::onFilterChanged);

    connect(listIssues, &QTreeWidget::itemClicked, this, &CodeReviewWidget::onItemClicked);
    connect(listIssues, &QTreeWidget::itemDoubleClicked, this, &CodeReviewWidget::onItemDoubleClicked);
}

void CodeReviewWidget::InitializeUI() {
    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(0, 0, 0, 0);
    mainLayout->setSpacing(0);

    auto* headerPanel = new QWidget(this);
    headerPanel->setObjectName("panelHeader");
    headerPanel->setFixedHeight(34);

    auto* headerLayout = new QHBoxLayout(headerPanel);
    headerLayout->setContentsMargins(5, 4, 5, 4);
    headerLayout->setSpacing(8);

    lblHeader = new QLabel("Code Review", headerPanel);
    QFont hf = lblHeader->font();
    hf.setBold(true);
    lblHeader->setFont(hf);

    cmbFilter = new QComboBox(headerPanel);
    cmbFilter->setFixedWidth(180);
    ThemeManager::SetupComboBox(cmbFilter, 14);

    lblStatus = new QLabel("No issues found.", headerPanel);
    lblStatus->setAlignment(Qt::AlignRight | Qt::AlignVCenter);

    headerLayout->addWidget(lblHeader);
    headerLayout->addWidget(cmbFilter);
    headerLayout->addStretch();
    headerLayout->addWidget(lblStatus);

    mainLayout->addWidget(headerPanel);

    listIssues = new QTreeWidget(this);
    listIssues->setHeaderLabels({"Sev", "Asset", "Title", "Description", "Ref"});
    listIssues->setRootIsDecorated(false);
    listIssues->setAllColumnsShowFocus(true);
    listIssues->setSelectionBehavior(QAbstractItemView::SelectRows);
    listIssues->setSelectionMode(QAbstractItemView::SingleSelection);
    listIssues->setMouseTracking(true);
    listIssues->installEventFilter(this);
    listIssues->viewport()->setMouseTracking(true);
    listIssues->viewport()->installEventFilter(this);

    listIssues->header()->setSectionResizeMode(QHeaderView::Interactive);
    listIssues->header()->setStretchLastSection(false);
    listIssues->setColumnWidth(0, 35);
    listIssues->setColumnWidth(1, 180);
    listIssues->setColumnWidth(2, 175);
    listIssues->setColumnWidth(3, 520);
    listIssues->setColumnWidth(4, 50);

    mainLayout->addWidget(listIssues, 1);
}

bool CodeReviewWidget::eventFilter(QObject* watched, QEvent* event) {
    if (watched == listIssues->viewport()) {
        if (event->type() == QEvent::MouseMove) {
            auto* mouseEvent = static_cast<QMouseEvent*>(event);
            QModelIndex idx = listIssues->indexAt(mouseEvent->pos());
            bool overLink = false;
            if (idx.isValid() && idx.column() == 4) {
                QTreeWidgetItem* item = listIssues->itemAt(mouseEvent->pos());
                if (item != nullptr) {
                    int issueIdx = item->data(0, Qt::UserRole).toInt();
                    if (issueIdx >= 0 && issueIdx < _filteredIssues.size() && !_filteredIssues[issueIdx].WikiUrl.isEmpty())
                        overLink = true;
                }
            }
            listIssues->viewport()->setCursor(overLink ? Qt::PointingHandCursor : Qt::ArrowCursor);
        } else if (event->type() == QEvent::Leave) {
            listIssues->viewport()->setCursor(Qt::ArrowCursor);
        }
    } else if (watched == listIssues && event->type() == QEvent::KeyPress) {
        auto* keyEvent = static_cast<QKeyEvent*>(event);
        if (keyEvent->key() == Qt::Key_Return || keyEvent->key() == Qt::Key_Enter) {
            TriggerIssueNavigation();
            return true;
        }
    }
    return QWidget::eventFilter(watched, event);
}

int CodeReviewWidget::FilterIndex() const { return cmbFilter ? cmbFilter->currentIndex() : 0; }

void CodeReviewWidget::SetFilterIndex(int index) {
    if (cmbFilter && index >= 0 && index < cmbFilter->count()) {
        cmbFilter->setCurrentIndex(index);
    }
}

void CodeReviewWidget::SetIssues(const QList<ValidationIssue>& issues) {
    _allIssues = issues;
    ApplyFilter();
}

void CodeReviewWidget::onFilterChanged() { ApplyFilter(); }

void CodeReviewWidget::ApplyFilter() {
    // Identify the selected issue by its full contents (asset + title + description
    // + reference), not just asset/title: many issues share the same title.
    auto IssueKey = [](const ValidationIssue& issue) -> QString {
        return QString("%1|%2|%3|%4|%5|%6|%7|%8|%9")
            .arg(static_cast<int>(issue.Severity))
            .arg(issue.Asset != nullptr ? issue.Asset->ID : 0)
            .arg(issue.Asset != nullptr && issue.Asset->isLeaderboard() ? 1 : 0)
            .arg(issue.Title, issue.Description, issue.WikiUrl, issue.SegmentName)
            .arg(issue.GroupIndex)
            .arg(issue.NoteAddress);
    };

    QString selectedKey;
    if (listIssues->currentItem() != nullptr) {
        int row = listIssues->indexOfTopLevelItem(listIssues->currentItem());
        if (row >= 0 && row < _filteredIssues.size())
            selectedKey = IssueKey(_filteredIssues[row]);
    }

    listIssues->setUpdatesEnabled(false);
    listIssues->clear();
    _filteredIssues.clear();

    int filterMode = cmbFilter->currentIndex();
    QSet<QString> seenMissingAddresses;

    auto sortedIssues = _allIssues;
    std::sort(sortedIssues.begin(), sortedIssues.end(), [](const ValidationIssue& a, const ValidationIssue& b) {
        if (a.Severity != b.Severity)
            return a.Severity > b.Severity;
        int aId = a.Asset != nullptr ? a.Asset->ID : 0;
        int bId = b.Asset != nullptr ? b.Asset->ID : 0;
        if (aId != bId)
            return aId < bId;
        return a.Title < b.Title;
    });

    for (int i = 0; i < sortedIssues.size(); i++) {
        const auto& issue = sortedIssues[i];
        bool isMissingNote = (issue.Title == "Undocumented Memory" || issue.Title == "Missing Code Note");

        if (filterMode == 1 && (isMissingNote || issue.Category != "Logic"))
            continue;
        if (filterMode == 2 && !isMissingNote)
            continue;
        if (filterMode == 3 && issue.Category != "Code Notes")
            continue;
        if (filterMode == 4 && issue.Category != "Presentation")
            continue;
        if (filterMode == 5 && issue.Category != "Set Design")
            continue;

        if (isMissingNote) {
            if (seenMissingAddresses.contains(issue.Description))
                continue;
            seenMissingAddresses.insert(issue.Description);
        }

        _filteredIssues.append(issue);
        int issueIndex = _filteredIssues.size() - 1;

        QString assetStr;
        if (issue.Asset != nullptr) {
            assetStr = issue.Asset->isLeaderboard() ? QString("L%1 - %2").arg(issue.Asset->ID).arg(issue.Asset->Title)
                                                    : QString("%1 - %2").arg(issue.Asset->ID).arg(issue.Asset->Title);
        } else if (!issue.NoteAddress.isEmpty()) {
            assetStr = "Code Notes";
        } else if (!issue.Category.isEmpty()) {
            assetStr = issue.Category;
        } else {
            assetStr = "Global";
        }

        auto* item = new QTreeWidgetItem();
        item->setText(0, GetSeverityIcon(issue.Severity));
        item->setText(1, assetStr);
        item->setText(2, issue.Title);
        item->setText(3, issue.Description);
        item->setText(4, !issue.WikiUrl.isEmpty() ? "Link" : "");
        item->setToolTip(1, assetStr);
        item->setToolTip(2, issue.Title);
        item->setToolTip(3, issue.Description);
        if (!issue.WikiUrl.isEmpty())
            item->setToolTip(4, issue.WikiUrl);

        item->setData(0, Qt::UserRole, issueIndex);

        if (issue.Severity >= FeedbackSeverity::Fail) {
            item->setForeground(0, Qt::red);
            item->setForeground(2, Qt::red);
        } else if (issue.Severity == FeedbackSeverity::Warning) {
            item->setForeground(0, QColor(255, 140, 0));
            item->setForeground(2, QColor(255, 140, 0));
        } else if (issue.Severity == FeedbackSeverity::Pass) {
            item->setForeground(0, QColor(46, 139, 87));
            item->setForeground(2, QColor(46, 139, 87));
        }

        if (!issue.WikiUrl.isEmpty()) {
            bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);
            item->setForeground(4, isDark ? QColor(88, 166, 255) : QColor(0, 102, 204));
            QFont f = item->font(4);
            f.setUnderline(true);
            item->setFont(4, f);
        }

        listIssues->addTopLevelItem(item);

        if (!selectedKey.isEmpty() && IssueKey(issue) == selectedKey) {
            listIssues->setCurrentItem(item);
            listIssues->scrollToItem(item);
        }
    }

    listIssues->setUpdatesEnabled(true);
    UpdateStatus(listIssues->topLevelItemCount());
}

void CodeReviewWidget::UpdateStatus(int count) {
    lblStatus->setText(count == 0 ? "No issues found." : QString("%1 issue(s) found.").arg(count));
    lblStatus->setStyleSheet(count == 0 ? "color: green;" : "");
}

QString CodeReviewWidget::GetSeverityIcon(FeedbackSeverity severity) const {
    switch (severity) {
    case FeedbackSeverity::Error:
    case FeedbackSeverity::Fail:
        return QString::fromUtf8("❌");
    case FeedbackSeverity::Warning:
        return QString::fromUtf8("⚠️");
    case FeedbackSeverity::Info:
        return QString::fromUtf8("ℹ️");
    case FeedbackSeverity::Pass:
        return QString::fromUtf8("✅");
    default:
        return "";
    }
}

void CodeReviewWidget::TriggerIssueNavigation() {
    auto* item = listIssues->currentItem();
    if (item != nullptr) {
        int idx = item->data(0, Qt::UserRole).toInt();
        if (idx >= 0 && idx < _filteredIssues.size()) {
            emit IssueSelected(_filteredIssues[idx]);
        }
    }
}

void CodeReviewWidget::onItemClicked(QTreeWidgetItem* item, int column) {
    if (item != nullptr && column == 4) {
        int idx = item->data(0, Qt::UserRole).toInt();
        if (idx >= 0 && idx < _filteredIssues.size() && !_filteredIssues[idx].WikiUrl.isEmpty()) {
            QDesktopServices::openUrl(QUrl(_filteredIssues[idx].WikiUrl));
        }
    }
}

void CodeReviewWidget::onItemDoubleClicked(QTreeWidgetItem* item, int column) {
    if (item != nullptr && column == 4) {
        int idx = item->data(0, Qt::UserRole).toInt();
        if (idx >= 0 && idx < _filteredIssues.size() && !_filteredIssues[idx].WikiUrl.isEmpty())
            return;
    }
    TriggerIssueNavigation();
}

} // namespace RALE
