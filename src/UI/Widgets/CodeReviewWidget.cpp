#include "CodeReviewWidget.h"

#include "../../Models/AssetModels.h"
#include "../Styles/ThemeManager.h"

#include <QAbstractTextDocumentLayout>
#include <QDesktopServices>
#include <QEvent>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QKeyEvent>
#include <QMouseEvent>
#include <QPainter>
#include <QResizeEvent>
#include <QSet>
#include <QStyledItemDelegate>
#include <QTextLayout>
#include <QTextOption>
#include <QUrl>
#include <QVBoxLayout>

#include <algorithm>

namespace RALE {

namespace {

constexpr int kRefColumnWidth = 46;
constexpr int kMinDescriptionWidth = 120;
constexpr int kCellPadH = 4;
constexpr int kCellPadV = 4;

class CodeReviewHeaderView : public QHeaderView {
  public:
    explicit CodeReviewHeaderView(Qt::Orientation orientation, QWidget* parent = nullptr) : QHeaderView(orientation, parent) {}

  protected:
    void paintSection(QPainter* painter, const QRect& rect, int logicalIndex) const override {
        if (logicalIndex != 4) {
            QHeaderView::paintSection(painter, rect, logicalIndex);
            return;
        }

        painter->save();
        QStyleOptionHeader opt;
        initStyleOption(&opt);
        opt.rect = rect;
        opt.section = logicalIndex;
        opt.text = "";
        style()->drawControl(QStyle::CE_HeaderSection, &opt, painter, this);

        const QString title = model() != nullptr ? model()->headerData(logicalIndex, orientation(), Qt::DisplayRole).toString() : QString();
        const bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);
        painter->setPen(isDark ? QColor(240, 240, 240) : QColor(0, 0, 0));
        painter->setFont(font());
        painter->drawText(rect.adjusted(4, 0, -6, 0), Qt::AlignRight | Qt::AlignVCenter, title);
        painter->restore();
    }
};

class CodeReviewItemDelegate : public QStyledItemDelegate {
  public:
    explicit CodeReviewItemDelegate(QObject* parent = nullptr) : QStyledItemDelegate(parent) {}

    QSize sizeHint(const QStyleOptionViewItem& option, const QModelIndex& index) const override {
        QStyleOptionViewItem opt = option;
        initStyleOption(&opt, index);

        if (index.column() == 3) {
            int colWidth = opt.rect.width();
            if (colWidth <= 0) {
                if (const auto* view = qobject_cast<const QTreeView*>(opt.widget))
                    colWidth = view->columnWidth(3);
            }
            const int textWidth = std::max(40, colWidth - kCellPadH * 2);

            QTextOption textOpt;
            textOpt.setWrapMode(QTextOption::WrapAtWordBoundaryOrAnywhere);
            QTextLayout layout(opt.text, opt.font);
            layout.setTextOption(textOpt);
            layout.beginLayout();
            qreal height = 0.0;
            while (true) {
                QTextLine line = layout.createLine();
                if (!line.isValid())
                    break;
                line.setLineWidth(textWidth);
                line.setPosition(QPointF(0, height));
                height += line.height();
            }
            layout.endLayout();

            const int totalH = std::max(22, static_cast<int>(std::ceil(height)) + kCellPadV * 2);
            return QSize(colWidth, totalH);
        }

        const QSize base = QStyledItemDelegate::sizeHint(option, index);
        return QSize(base.width(), std::max(22, base.height() + 2));
    }

    void paint(QPainter* painter, const QStyleOptionViewItem& option, const QModelIndex& index) const override {
        QStyleOptionViewItem opt = option;
        initStyleOption(&opt, index);

        // Let QStyle draw selection/hover/focus background without drawing text
        QStyleOptionViewItem bgOpt = opt;
        bgOpt.text.clear();
        if (opt.widget != nullptr) {
            opt.widget->style()->drawControl(QStyle::CE_ItemViewItem, &bgOpt, painter, opt.widget);
        } else {
            QStyledItemDelegate::paint(painter, bgOpt, index);
        }

        if (opt.text.isEmpty())
            return;

        QColor textColor;
        if (opt.state & QStyle::State_Selected) {
            textColor = opt.palette.highlightedText().color();
        } else {
            const QVariant fg = index.data(Qt::ForegroundRole);
            textColor = fg.isValid() ? fg.value<QColor>() : opt.palette.text().color();
        }

        painter->save();
        painter->setPen(textColor);
        painter->setFont(opt.font);
        const QRect textRect = opt.rect.adjusted(kCellPadH, kCellPadV, -kCellPadH, -kCellPadV);
        painter->setClipRect(opt.rect.adjusted(1, 1, -1, -1));

        if (index.column() == 3) {
            QTextOption textOpt;
            textOpt.setWrapMode(QTextOption::WrapAtWordBoundaryOrAnywhere);
            QTextLayout layout(opt.text, opt.font);
            layout.setTextOption(textOpt);
            layout.beginLayout();
            qreal y = 0.0;
            while (true) {
                QTextLine line = layout.createLine();
                if (!line.isValid())
                    break;
                line.setLineWidth(std::max(20, textRect.width()));
                line.setPosition(QPointF(0, y));
                y += line.height();
            }
            layout.endLayout();
            layout.draw(painter, QPointF(textRect.left(), textRect.top()));
        } else if (index.column() == 4) {
            painter->drawText(opt.rect.adjusted(kCellPadH, kCellPadV, -6, -kCellPadV), Qt::AlignRight | Qt::AlignTop, opt.text);
        } else if (index.column() == 0) {
            painter->drawText(textRect, Qt::AlignCenter | Qt::AlignTop, opt.text);
        } else {
            const QString elided = opt.fontMetrics.elidedText(opt.text, Qt::ElideRight, std::max(10, textRect.width()));
            painter->drawText(textRect, Qt::AlignLeft | Qt::AlignTop, elided);
        }

        painter->restore();
    }
};

} // namespace

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
    connect(listIssues, &QTreeWidget::itemSelectionChanged, this, &CodeReviewWidget::onSelectionChanged);
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

    btnSuggestedFix = new QPushButton("Suggested Fix...", headerPanel);
    btnSuggestedFix->setFixedHeight(24);
    btnSuggestedFix->setEnabled(false);
    btnSuggestedFix->setToolTip("Preview and apply an automated fix for the selected issue");
    connect(btnSuggestedFix, &QPushButton::clicked, this, &CodeReviewWidget::onSuggestedFixClicked);

    lblStatus = new QLabel("No issues found.", headerPanel);
    lblStatus->setAlignment(Qt::AlignRight | Qt::AlignVCenter);

    headerLayout->addWidget(lblHeader);
    headerLayout->addWidget(cmbFilter);
    headerLayout->addWidget(btnSuggestedFix);
    headerLayout->addStretch();
    headerLayout->addWidget(lblStatus);

    mainLayout->addWidget(headerPanel);

    listIssues = new QTreeWidget(this);
    listIssues->setHeader(new CodeReviewHeaderView(Qt::Horizontal, listIssues));
    listIssues->setItemDelegate(new CodeReviewItemDelegate(listIssues));
    listIssues->setHeaderLabels({"Sev", "Asset", "Title", "Description", "Ref"});
    listIssues->setRootIsDecorated(false);
    listIssues->setAllColumnsShowFocus(true);
    listIssues->setSelectionBehavior(QAbstractItemView::SelectRows);
    listIssues->setSelectionMode(QAbstractItemView::SingleSelection);
    listIssues->setWordWrap(true);
    listIssues->setUniformRowHeights(false);
    listIssues->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    listIssues->setMouseTracking(true);
    listIssues->installEventFilter(this);
    listIssues->viewport()->setMouseTracking(true);
    listIssues->viewport()->installEventFilter(this);

    auto* hdr = listIssues->header();
    hdr->setStretchLastSection(false);
    hdr->setMinimumSectionSize(28);
    hdr->setSectionResizeMode(0, QHeaderView::Interactive);
    hdr->setSectionResizeMode(1, QHeaderView::Interactive);
    hdr->setSectionResizeMode(2, QHeaderView::Interactive);
    hdr->setSectionResizeMode(3, QHeaderView::Stretch);
    hdr->setSectionResizeMode(4, QHeaderView::Fixed);

    listIssues->setColumnWidth(0, 35);
    listIssues->setColumnWidth(1, 175);
    listIssues->setColumnWidth(2, 175);
    listIssues->setColumnWidth(4, kRefColumnWidth);
    listIssues->headerItem()->setTextAlignment(4, Qt::AlignRight | Qt::AlignVCenter);

    connect(hdr, &QHeaderView::sectionResized, this, &CodeReviewWidget::onHeaderSectionResized);

    _contextMenu = new QMenu(this);
    _actSuggestedFix = _contextMenu->addAction("Apply Suggested Fix...", this, &CodeReviewWidget::onSuggestedFixClicked);
    _actGoToIssue = _contextMenu->addAction("Go to Issue", this, &CodeReviewWidget::TriggerIssueNavigation);
    _actOpenLink = _contextMenu->addAction("Open Reference Link", this, [this]() {
        const auto* issue = CurrentSelectedIssue();
        if (issue != nullptr && !issue->WikiUrl.isEmpty())
            QDesktopServices::openUrl(QUrl(issue->WikiUrl));
    });

    listIssues->setContextMenuPolicy(Qt::CustomContextMenu);
    connect(listIssues, &QWidget::customContextMenuRequested, this, [this](const QPoint& pos) {
        const auto* issue = CurrentSelectedIssue();
        if (issue == nullptr)
            return;
        _actSuggestedFix->setEnabled(issue->HasSuggestedFix());
        _actGoToIssue->setEnabled(issue->Asset != nullptr || !issue->NoteAddress.isEmpty());
        _actOpenLink->setEnabled(!issue->WikiUrl.isEmpty());
        _contextMenu->exec(listIssues->viewport()->mapToGlobal(pos));
    });

    mainLayout->addWidget(listIssues, 1);
}

void CodeReviewWidget::resizeEvent(QResizeEvent* event) {
    QWidget::resizeEvent(event);
    FitColumnsToViewport();
}

void CodeReviewWidget::onHeaderSectionResized(int logicalIndex, int /*oldSize*/, int newSize) {
    if (_isAdjustingColumns)
        return;

    if (logicalIndex == 0 || logicalIndex == 1 || logicalIndex == 2) {
        const int avail = listIssues->viewport()->width();
        if (avail > 0) {
            const int w0 = listIssues->columnWidth(0);
            const int w1 = listIssues->columnWidth(1);
            const int w2 = listIssues->columnWidth(2);
            const int maxPrefix = std::max(90, avail - kRefColumnWidth - kMinDescriptionWidth);
            if (w0 + w1 + w2 > maxPrefix) {
                _isAdjustingColumns = true;
                const int overflow = (w0 + w1 + w2) - maxPrefix;
                listIssues->setColumnWidth(logicalIndex, std::max(28, newSize - overflow));
                _isAdjustingColumns = false;
            }
        }
    }

    if (logicalIndex <= 3) {
        listIssues->doItemsLayout();
    }
}

void CodeReviewWidget::FitColumnsToViewport() {
    if (_isAdjustingColumns || listIssues == nullptr)
        return;

    const int avail = listIssues->viewport()->width();
    if (avail <= 0)
        return;

    _isAdjustingColumns = true;
    const int maxPrefix = std::max(90, avail - kRefColumnWidth - kMinDescriptionWidth);
    int w0 = listIssues->columnWidth(0);
    int w1 = listIssues->columnWidth(1);
    int w2 = listIssues->columnWidth(2);

    if (w0 + w1 + w2 > maxPrefix) {
        int excess = (w0 + w1 + w2) - maxPrefix;
        const int take2 = std::min(excess, std::max(0, w2 - 60));
        w2 -= take2;
        excess -= take2;
        const int take1 = std::min(excess, std::max(0, w1 - 60));
        w1 -= take1;
        excess -= take1;
        if (excess > 0)
            w0 = std::max(28, w0 - excess);

        listIssues->setColumnWidth(0, w0);
        listIssues->setColumnWidth(1, w1);
        listIssues->setColumnWidth(2, w2);
    }
    _isAdjustingColumns = false;

    listIssues->doItemsLayout();
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
        } else if (event->type() == QEvent::Resize) {
            FitColumnsToViewport();
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

        const QString titleDisplay = issue.HasSuggestedFix() ? (QString::fromUtf8("💡 ") + issue.Title) : issue.Title;

        auto* item = new QTreeWidgetItem();
        item->setText(0, GetSeverityIcon(issue.Severity));
        item->setText(1, assetStr);
        item->setText(2, titleDisplay);
        item->setText(3, issue.Description);
        item->setText(4, !issue.WikiUrl.isEmpty() ? "Link" : "");
        item->setTextAlignment(4, Qt::AlignRight | Qt::AlignVCenter);
        item->setToolTip(1, assetStr);
        item->setToolTip(2, issue.HasSuggestedFix() ? QString("%1 (Suggested fix available)").arg(issue.Title) : issue.Title);
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
    listIssues->doItemsLayout();
    UpdateStatus(listIssues->topLevelItemCount());
    UpdateSuggestedFixButton();
}

void CodeReviewWidget::UpdateStatus(int count) {
    lblStatus->setText(count == 0 ? "No issues found." : QString("%1 issue(s) found.").arg(count));
    lblStatus->setStyleSheet(count == 0 ? "color: green;" : "");
}

const ValidationIssue* CodeReviewWidget::CurrentSelectedIssue() const {
    auto* item = listIssues->currentItem();
    if (item == nullptr)
        return nullptr;
    const int idx = item->data(0, Qt::UserRole).toInt();
    if (idx < 0 || idx >= _filteredIssues.size())
        return nullptr;
    return &_filteredIssues[idx];
}

void CodeReviewWidget::UpdateSuggestedFixButton() {
    if (btnSuggestedFix == nullptr)
        return;
    const auto* issue = CurrentSelectedIssue();
    const bool hasFix = (issue != nullptr && issue->HasSuggestedFix());
    btnSuggestedFix->setEnabled(hasFix);
    btnSuggestedFix->setText(hasFix ? QString::fromUtf8("💡 Suggested Fix...") : "Suggested Fix...");
}

void CodeReviewWidget::onSelectionChanged() { UpdateSuggestedFixButton(); }

void CodeReviewWidget::onSuggestedFixClicked() {
    const auto* issue = CurrentSelectedIssue();
    if (issue != nullptr && issue->HasSuggestedFix())
        emit SuggestedFixRequested(*issue);
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
    const auto* issue = CurrentSelectedIssue();
    if (issue != nullptr) {
        emit IssueSelected(*issue);
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
