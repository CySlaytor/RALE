#include "TitleCaseAnalyzer.h"

#include <QMap>
#include <QRegularExpression>
#include <QSet>
#include <QStringList>

#include <functional>
#include <optional>

namespace RALE {

namespace {

QSet<QString> MakeWordSet(const QString& list) {
    QSet<QString> set;
    const QStringList parts = list.trimmed().split(QRegularExpression("\\s+"), Qt::SkipEmptyParts);
    for (const QString& p : parts)
        set.insert(p);
    return set;
}

template <typename... Sets> QSet<QString> UnionSets(const Sets&... sets) {
    QSet<QString> out;
    ((out.unite(sets)), ...);
    return out;
}

QSet<QString> WithoutWord(const QSet<QString>& group, const QString& excluded) {
    QSet<QString> copy = group;
    copy.remove(excluded);
    return copy;
}

// --- Word groups ---

const QSet<QString>& Articles() {
    static const QSet<QString> s = MakeWordSet("a an the");
    return s;
}

const QSet<QString>& CoordinatingConjunctions() {
    static const QSet<QString> s = MakeWordSet("and but for or nor");
    return s;
}

const QSet<QString>& ShortPrepositions() {
    static const QSet<QString> s = MakeWordSet("at of to for into onto unto from with amid upon qua sans atop vs v via per");
    return s;
}

const QSet<QString>& LongPrepositions() {
    static const QSet<QString> s = MakeWordSet(
        "about above across against along among around beneath beside besides between beyond despite during except "
        "inside outside past through throughout toward towards under underneath unlike versus within without");
    return s;
}

const QSet<QString>& Particles() {
    static const QSet<QString> s = MakeWordSet("up out off on in by over down");
    return s;
}

const QSet<QString>& AdverbBiased() {
    static const QSet<QString> s = MakeWordSet("up out");
    return s;
}

const QSet<QString>& Subordinators() {
    static const QSet<QString> s = MakeWordSet("when where while until till before after because though although as if unless whether since than like");
    return s;
}

const QSet<QString>& SubjectPronouns() {
    static const QSet<QString> s = MakeWordSet("i you we they he she it");
    return s;
}

const QSet<QString>& ObjectPronouns() {
    static const QSet<QString> s = MakeWordSet("me him her them us it you 'em");
    return s;
}

const QSet<QString>& Demonstratives() {
    static const QSet<QString> s = MakeWordSet("this that these those");
    return s;
}

const QSet<QString>& Possessives() {
    static const QSet<QString> s = MakeWordSet("my your his her its our their whose");
    return s;
}

const QSet<QString>& Quantifiers() {
    static const QSet<QString> s = MakeWordSet("some any every each no all both");
    return s;
}

const QSet<QString>& BeVerbs() {
    static const QSet<QString> s = MakeWordSet("am is are was were be been being isn't aren't wasn't weren't");
    return s;
}

const QSet<QString>& Auxiliaries() {
    static const QSet<QString> s = UnionSets(
        BeVerbs(),
        MakeWordSet("do don't does doesn't did didn't has have had "
                    "will won't would wouldn't could couldn't should shouldn't can can't cannot may might must shall "
                    "gonna gotta wanna"));
    return s;
}

const QSet<QString>& Adverbs() {
    static const QSet<QString> s = MakeWordSet(
        "so too very now then again anytime anywhere anymore soon fast quick quickly slowly later tonight today "
        "tomorrow yesterday forever never ever always already yet still away next first last early late once "
        "twice more most much well far near close ahead behind below above ago also even only just really maybe perhaps");
    return s;
}

// --- Sets built from the groups ---

const QSet<QString>& Determiners() {
    static const QSet<QString> s = UnionSets(Articles(), Demonstratives(), Possessives(), Quantifiers());
    return s;
}

const QSet<QString>& ClauseStarters() {
    static const QSet<QString> s = UnionSets(SubjectPronouns(), Auxiliaries(), QSet<QString>{"who"});
    return s;
}

const QSet<QString>& NotAnObject() {
    static const QSet<QString> s = UnionSets(Adverbs(), Auxiliaries(), CoordinatingConjunctions(), Subordinators(), ShortPrepositions(), LongPrepositions());
    return s;
}

const QSet<QString>& PhrasalVerbObjects() {
    static const QSet<QString> s = UnionSets(ObjectPronouns(), Demonstratives());
    return s;
}

const QSet<QString>& LikePrepositionAfter() {
    static const QSet<QString> s = UnionSets(
        WithoutWord(ObjectPronouns(), "you"),
        BeVerbs(),
        MakeWordSet("it's that's just more nothing run fight walk live ride shine built bright place "
                    "smell smells smelled look looks looked taste tastes tasted feel feels felt eat ate"));
    return s;
}

// --- Other tables ---

const QSet<QString>& Abbreviations() {
    static const QSet<QString> s = MakeWordSet("vs v etc mr mrs ms dr st jr sr no vol ft mt inc ltd co feat ep op pt ch");
    return s;
}

const QMap<QString, QSet<QString>>& PhrasalVerbs() {
    static const QMap<QString, QSet<QString>> m = {
        {"up", MakeWordSet("level power wake give brighten bang bust send end step bump set pick hurry speed clean catch light line load "
                           "lock mix pump round shut sign stock suit team warm wind wrap back blow break build burn cheer cook cough crank "
                           "dig dress drink eat fill fire fix free gear hang heat hold hook keep look make open pair pay pile rack rev save "
                           "scale screw shake shape size split stack start stir tear throw tidy turn use wash whip beef boot bottle bring "
                           "buck buckle bulk bundle butter buy call charge chalk chop clam clear clog close conjure cover crack cuddle curl "
                           "cut dial divide double draw drum dry ease finish fluff fold follow gas gobble gun hush jack juice kick knock "
                           "lace lap lift link liven mark measure mess mop muck muster own patch perk pin plug polish prop pull push put "
                           "rake ramp read rig ring roll rustle scoop scrape scrounge seal serve sew shore shoot shrivel slice smarten snap "
                           "soak soup spark spice spruce square stick straighten strike suck sum swallow sweep swell take talk tally tank "
                           "tee thaw think tie tighten toss total touch toughen trade trump tune vamp wall wear weigh wipe work write zip "
                           "show")},
        {"out", MakeWordSet("take check figure find point pull rat rule sort try turn wipe work max run hang lock pick play roll shoot sit "
                            "space spread stretch wear act carry cut keep leave blow clean clear cross deal dish dole drag draw drown dry "
                            "eke fan farm fill fish flesh flush get grind hammer hand hash help hide iron kick knock lay let make map mete "
                            "phase plan pour print push put rent ride root rub scope seek sell send set shell ship shout single smoke "
                            "smooth snuff sound spell spit stamp stick stress strike sweat sweep talk tap test thin throw thrash trot tune "
                            "vote wait wash weed wring write block bring buy call cast chuck churn count crank cry dig dust edit flag flat "
                            "flip freeze give hold hunt ink jut kill laugh live lure miss move opt pace pass pay pipe plot press punch "
                            "reach read rip rough scratch screen scream scrub search seal shake share shut sketch sleep slip snap sniff spin "
                            "spot squeeze stake stare storm straighten string strip suss swap tease thrust tire toss trace trick whip win "
                            "yank zone zoom")},
        {"off", MakeWordSet("turn take show back blast brush call cool cut fend fight finish give hold kick knock lay lift log pay pull put "
                            "rip see sell set shake shave shrug sign sound square start switch tell throw tip top ward wear write auction "
                            "bite blow cap cast check clear cross dust ease fire fob hit keep lead leave make mark pair peel pick polish "
                            "power rattle reel round rub scare scrape seal send shoot shut siphon skim sleep slack sod spin split spark "
                            "stave strike tail tick toss touch trigger wave wipe work bugger buzz fuck piss dash head hop kill laugh live "
                            "nod pass ride sail scrub sweat wash")},
        {"on", MakeWordSet("carry keep put turn switch bring cheer lead pass power spur urge try add drag press ramble rattle read strap "
                           "tack tag")},
        {"in", MakeWordSet("tune cash plug give kick pull rake reel sign take throw tuck usher zero break bring call fill lock log send")},
        {"over", MakeWordSet("take turn knock look pass push win think talk mull hand fork gloss paint paper plaster read sign smooth start "
                             "do make carry chew check kick pick tip flip bowl bring change hold run sleep switch voice work")},
        {"down", MakeWordSet("calm break burn chow cut hunt knock lay let lock mow nail pin power pull put run shoot shut slow take tear "
                             "throw tone track turn wear wind write back boil bring chase dumb hand load mark pass play ram scale water wolf "
                             "bog bolt chop clamp close crack drink face gun hammer hose kick narrow pare peg ring rub sand scrub send set "
                             "shake shout slam slap smack snap strip sweep talk tamp thin tie wash weigh wipe count double hold keep live "
                             "cool quiet strike bear buckle head simmer bed bow bunk call cast crash dress flag flop plunk stamp buy die jot "
                             "melt phase pipe press push rain roll settle stare step stomp tramp trickle wave")},
    };
    return m;
}

const QMap<QString, QString>& IrregularVerbs() {
    static const QMap<QString, QString> m = []() {
        QMap<QString, QString> out;
        const QSet<QString> pairs = MakeWordSet(
            "took:take taken:take gave:give given:give got:get gotten:get went:go gone:go came:come broke:break broken:break "
            "blew:blow blown:blow grew:grow grown:grow held:hold hung:hang kept:keep knew:know known:know lit:light made:make "
            "paid:pay ran:run rose:rise risen:rise shook:shake shaken:shake shot:shoot sold:sell spoke:speak spoken:speak "
            "stood:stand stuck:stick struck:strike swept:sweep threw:throw thrown:throw told:tell tore:tear torn:tear "
            "woke:wake woken:wake wore:wear worn:wear wrote:write written:write won:win wound:wind drew:draw drawn:draw "
            "ate:eat eaten:eat fell:fall fallen:fall fought:fight found:find flew:fly flown:fly froze:freeze frozen:freeze "
            "led:lead lay:lie lain:lie left:leave lost:lose met:meet rode:ride ridden:ride rang:ring rung:ring sat:sit "
            "saw:see seen:see sent:send sang:sing sung:sing slept:sleep spent:spend sprang:spring stole:steal stolen:steal "
            "swam:swim swum:swim brought:bring bought:buy caught:catch did:do done:do dug:dig fed:feed hid:hide hidden:hide "
            "lent:lend built:build bound:bind bent:bend bled:bleed bred:breed burnt:burn dealt:deal dreamt:dream fled:flee "
            "knelt:kneel laid:lay meant:mean said:say sought:seek slid:slide sped:speed sank:sink sunk:sink swung:swing "
            "taught:teach thought:think understood:understand wept:weep beat:beat beaten:beat became:become began:begin "
            "begun:begin bit:bite bitten:bite chose:choose chosen:choose drank:drink drunk:drink drove:drive driven:drive "
            "forgot:forget forgotten:forget shrank:shrink shrunk:shrink spun:spin stank:stink swore:swear sworn:swear "
            "clung:cling crept:creep slung:sling spat:spit strung:string stung:sting stunk:stink swelled:swell swollen:swell "
            "trod:tread leapt:leap shone:shine sewn:sew mown:mow sawn:saw hewn:hew shorn:shear slain:slay slew:slay "
            "wrung:wring overcame:overcome undid:undo undone:undo withdrew:withdraw");
        for (const QString& pair : pairs) {
            const int colon = pair.indexOf(':');
            if (colon > 0)
                out.insert(pair.left(colon), pair.mid(colon + 1));
        }
        return out;
    }();
    return m;
}

struct Decision {
    std::optional<bool> Cap; // true = capitalize, false = lowercase, nullopt = keep
    QString Reason;
};

Decision Capital(const QString& reason) { return {true, reason}; }
Decision Lower(const QString& reason) { return {false, reason}; }
Decision Keep(const QString& reason) { return {std::nullopt, reason}; }

const QString AS_PREPOSITION = "seems to be used as a preposition";
const QString AS_LETTER = "is used as a letter (noun)";
const QString AS_NOUN_OR_ADJECTIVE = "is used as a noun or adjective";
const QString DEFAULT_REASON = "is neither an article, a coordinating conjunction nor a preposition of four letters or fewer";

const QMap<QString, QString>& LowercaseReasons() {
    static const QMap<QString, QString> m = []() {
        QMap<QString, QString> out;
        auto addGroup = [&out](const QString& reason, const QSet<QString>& keys) {
            for (const QString& k : keys) {
                if (!out.contains(k))
                    out.insert(k, reason);
            }
        };
        addGroup("is an article", Articles());
        addGroup("is \"as\", which Chicago style always lowercases", QSet<QString>{"as"});
        addGroup("is a preposition or part of an infinitive", QSet<QString>{"to"});
        addGroup("is a preposition of four letters or fewer", ShortPrepositions());
        addGroup("is a coordinating conjunction", CoordinatingConjunctions());
        return out;
    }();
    return m;
}

// --- Regexes (PCRE2 Unicode Properties) ---

const QRegularExpression& WordRe() {
    static const QRegularExpression re(
        QString::fromUtf8(R"([\p{L}\p{N}'\x{2019}]+(?:\.(?=[\p{L}\p{N}])[\p{L}\p{N}'\x{2019}]+)*)"),
        QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& ClauseStartRe() {
    static const QRegularExpression re(
        QString::fromUtf8(R"([!?:;)\]}/|]|(?<![.])\.(?![.])|["\x{201C}\x{2018}'\x{2019}]$)"),
        QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& ClauseEndRe() {
    static const QRegularExpression re(
        QString::fromUtf8(R"([!?:;()[\]{}/|]|(?<![.])\.(?![.])|^["\x{201D}\x{2019}'])"),
        QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& TrailingDashRe() {
    static const QRegularExpression re(
        QString::fromUtf8(R"([-\x{2010}\x{2011}\x{2012}\x{2013}\x{2014}\x{2015}]\s*$)"),
        QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& LeadingEllipsisRe() {
    static const QRegularExpression re(
        QString::fromUtf8(R"((?:\.\.\.|\x{2026})\s*$)"),
        QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& HyphenRe() {
    static const QRegularExpression re(
        QString::fromUtf8(R"(^[-\x{2010}\x{2011}]$)"),
        QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& WhitespaceRe() {
    static const QRegularExpression re(R"(^\s+$)", QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& OpeningQuoteRe() {
    static const QRegularExpression re(
        QString::fromUtf8(R"(^\s+["\x{201C}\x{2018}'\x{2019}([]+$)"),
        QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& NonLatinRe() {
    static const QRegularExpression re(R"((?![\p{Latin}])\p{L})", QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& InitialismRe() {
    static const QRegularExpression re(R"(^(?:\p{L}\.)*\p{L}$)", QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& LeadingApostropheRe() {
    static const QRegularExpression re(
        QString::fromUtf8(R"(^['\x{2019}](?:\p{N}|(?:em|n|til|till|bout|cause|round|tis|twas|neath|nuff)['\x{2019}]?$))"),
        QRegularExpression::CaseInsensitiveOption | QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& ClippedOfRe() {
    static const QRegularExpression re(QString::fromUtf8(R"(^o['\x{2019}]$)"), QRegularExpression::CaseInsensitiveOption);
    return re;
}

const QRegularExpression& ClippedAndRe() {
    static const QRegularExpression re(QString::fromUtf8(R"(^(?:['\x{2019}]n['\x{2019}]?|n['\x{2019}])$)"),
                                       QRegularExpression::CaseInsensitiveOption);
    return re;
}

const QRegularExpression& ClippedAboutRe() {
    static const QRegularExpression re(QString::fromUtf8(R"(^['\x{2019}]bout$)"), QRegularExpression::CaseInsensitiveOption);
    return re;
}

const QRegularExpression& VerbFormRe() {
    static const QRegularExpression re(R"((?:ing|in'|ed)$)");
    return re;
}

const QRegularExpression& LetterRe() {
    static const QRegularExpression re(R"(\p{L})", QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& DigitRe() {
    static const QRegularExpression re(R"(\p{N})", QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& NonLetterRe() {
    static const QRegularExpression re(R"([^\p{L}])", QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

const QRegularExpression& UpperLetterRe() {
    static const QRegularExpression re(R"(\p{Lu})", QRegularExpression::UseUnicodePropertiesOption);
    return re;
}

struct Token {
    QString Joint;
    QString Word;
};

struct TokenizedTitle {
    QList<Token> Tokens;
    QString LastJoint;
};

TokenizedTitle Tokenize(const QString& phrase) {
    TokenizedTitle out;
    int last = 0;
    bool quoteOpen = false;

    static const QRegularExpression openSingleQuoteRe(QString::fromUtf8(R"(\x{2018}\s*$)"),
                                                      QRegularExpression::UseUnicodePropertiesOption);
    static const QRegularExpression leadAposStripRe(QString::fromUtf8(R"(^['\x{2019}]*)"));
    static const QRegularExpression trailAposStripRe(QString::fromUtf8(R"(['\x{2019}]*$)"));

    auto it = WordRe().globalMatch(phrase);
    while (it.hasNext()) {
        auto match = it.next();
        QString word = match.captured(0);
        int start = static_cast<int>(match.capturedStart(0));
        int end = static_cast<int>(match.capturedEnd(0));

        const int leading = LeadingApostropheRe().match(word).hasMatch()
                                ? 0
                                : static_cast<int>(leadAposStripRe.match(word).capturedLength(0));
        if (leading > 0 || openSingleQuoteRe.match(phrase.mid(last, start - last)).hasMatch())
            quoteOpen = true;

        const int trailing = quoteOpen ? static_cast<int>(trailAposStripRe.match(word).capturedLength(0)) : 0;
        if (trailing > 0)
            quoteOpen = false;

        if (leading + trailing >= word.length())
            continue;

        word = word.mid(leading, word.length() - leading - trailing);
        start += leading;
        end -= trailing;

        const bool abbreviation = Abbreviations().contains(word.toLower()) || InitialismRe().match(word).hasMatch();
        if (end < phrase.length() && phrase[end] == '.' && (end + 1 >= phrase.length() || phrase[end + 1] != '.') && abbreviation) {
            word += '.';
            end++;
        }

        out.Tokens.append({phrase.mid(last, start - last), word});
        last = end;
    }

    out.LastJoint = phrase.mid(last);
    return out;
}

QString LookupKey(const QString& word) {
    QString k = word.toLower().replace(QChar(0x2019), '\'');
    if (k.endsWith('.'))
        k.chop(1);
    return k;
}

QString CapitalizeWord(const QString& word) {
    const auto m = LetterRe().match(word);
    if (!m.hasMatch())
        return word;
    const int i = static_cast<int>(m.capturedStart(0));
    return word.left(i) + word[i].toUpper() + word.mid(i + 1).toLower();
}

bool IsAllCaps(const QString& word) {
    QString letters = word;
    letters.remove(NonLetterRe());
    return letters.length() >= 2 && letters == letters.toUpper();
}

bool HasInnerCapital(const QString& word) {
    const auto m = LetterRe().match(word);
    if (!m.hasMatch())
        return false;
    const int i = static_cast<int>(m.capturedStart(0));
    QString rest = word.mid(i + 1);
    rest.remove(NonLetterRe());
    return UpperLetterRe().match(rest).hasMatch();
}

QSet<QString> VerbStems(const QString& word) {
    const QString w = word.toLower().replace(QChar(0x2019), '\'');
    QSet<QString> stems{w};
    auto add = [&stems](const QString& stem) {
        if (stem.length() >= 2)
            stems.insert(stem);
    };
    auto doubled = [&w](const QString& suffix) -> bool {
        return w.length() > suffix.length() + 2 && w.at(w.length() - suffix.length() - 1) == w.at(w.length() - suffix.length() - 2);
    };

    if (IrregularVerbs().contains(w))
        stems.insert(IrregularVerbs().value(w));
    if (w.endsWith("ies") || w.endsWith("ied"))
        add(w.left(w.length() - 3) + "y");
    if (w.endsWith("es"))
        add(w.left(w.length() - 2));
    if (w.endsWith("s"))
        add(w.left(w.length() - 1));
    if (w.endsWith("ed")) {
        add(w.left(w.length() - 2));
        add(w.left(w.length() - 1));
        if (doubled("ed"))
            add(w.left(w.length() - 3));
    }
    if (w.endsWith("ing")) {
        add(w.left(w.length() - 3));
        add(w.left(w.length() - 3) + "e");
        if (doubled("ing"))
            add(w.left(w.length() - 4));
    }
    if (w.endsWith("in'")) {
        add(w.left(w.length() - 3));
        add(w.left(w.length() - 3) + "e");
    }
    return stems;
}

class ParsedTitle {
  public:
    QList<Token> Tokens;
    QString LastJoint;
    QStringList Keys;

    explicit ParsedTitle(const QString& phrase) {
        const auto tokenized = Tokenize(phrase);
        Tokens = tokenized.Tokens;
        LastJoint = tokenized.LastJoint;
        Keys.reserve(Tokens.size());
        for (const auto& t : Tokens)
            Keys.append(LookupKey(t.Word));
    }

    int Length() const { return static_cast<int>(Tokens.size()); }
    const QString& Word(int i) const { return Tokens[i].Word; }
    const QString& JointBefore(int i) const { return i < Length() ? Tokens[i].Joint : LastJoint; }

    bool StartsClause(int i) const {
        const QString& joint = Tokens[i].Joint;
        if (i == 0)
            return !LeadingEllipsisRe().match(joint).hasMatch();
        return !TrailingDashRe().match(joint).hasMatch() && ClauseStartRe().match(joint).hasMatch();
    }

    bool EndsClause(int i) const { return i == Length() - 1 || ClauseEndRe().match(Tokens[i + 1].Joint).hasMatch(); }

    QString PrevKey(int i) const { return (i > 0 && !StartsClause(i)) ? Keys[i - 1] : QString(); }
    QString NextKey(int i) const { return (i + 1 < Length() && !StartsClause(i + 1)) ? Keys[i + 1] : QString(); }

    QString AdjacentPrev(int i) const { return PlainBefore(i) ? Keys[i - 1] : QString(); }
    QString AdjacentNext(int i) const { return PlainAfter(i) ? Keys[i + 1] : QString(); }

    bool PlainBefore(int i) const { return i > 0 && WhitespaceRe().match(Tokens[i].Joint).hasMatch(); }
    bool PlainAfter(int i) const { return i + 1 < Length() && WhitespaceRe().match(Tokens[i + 1].Joint).hasMatch(); }

    bool HyphenBefore(int i) const { return HyphenRe().match(Tokens[i].Joint).hasMatch(); }
    bool HyphenAfter(int i) const { return i + 1 < Length() && HyphenRe().match(Tokens[i + 1].Joint).hasMatch(); }
    bool QuoteAfter(int i) const { return i + 1 < Length() && OpeningQuoteRe().match(Tokens[i + 1].Joint).hasMatch(); }

    bool FollowedByObject(int i) const {
        const QString next = NextKey(i);
        if (next.isEmpty())
            return QuoteAfter(i);
        if (!PlainAfter(i))
            return false;
        if (NotAnObject().contains(next))
            return false;
        return !(Particles().contains(next) && AdverbBiased().contains(Keys[i]));
    }

    bool PrecededByPhrasalVerb(int i) const {
        const QString& particle = Keys[i];
        auto it = PhrasalVerbs().constFind(particle);
        if (it == PhrasalVerbs().constEnd())
            return false;
        const QSet<QString>& verbs = it.value();

        int verb = i - 1;
        if (particle != "down" && verb >= 0 && !StartsClause(i) && PhrasalVerbObjects().contains(Keys[verb]))
            verb--;

        if (verb < 0 || StartsClause(verb + 1) || !PlainBefore(verb + 1))
            return false;

        const QSet<QString> stems = VerbStems(Word(verb));
        for (const QString& stem : stems) {
            if (verbs.contains(stem))
                return true;
        }
        return false;
    }
};

Decision DecideParticle(const ParsedTitle& title, int i) {
    const QString& particle = title.Keys[i];
    const QString next = title.NextKey(i);

    if (title.HyphenBefore(i)) {
        return title.HyphenAfter(i) ? Lower("is a preposition inside a hyphenated compound")
                                    : Capital("is an adverbial particle ending a hyphenated compound");
    }
    if (title.PrecededByPhrasalVerb(i))
        return Capital("seems to be used as an adverb (phrasal verb)");
    if (title.AdjacentNext(i) == "of")
        return Lower("is part of a compound preposition (\"out of\")");
    if (particle == "by" && (next == "now" || next == "then" || next == "far"))
        return Lower("is part of a compound preposition (\"by now\")");
    if (AdverbBiased().contains(particle) && VerbFormRe().match(title.AdjacentPrev(i)).hasMatch())
        return Capital("seems to be used as an adverb (after a verb form)");
    if (!title.FollowedByObject(i))
        return Capital("seems to be used as an adverb (no object follows)");
    return Lower(AS_PREPOSITION);
}

Decision DecidePastOrPlus(const ParsedTitle& title, int i, const QString& /*prev*/, const QString& next) {
    if (Determiners().contains(title.AdjacentPrev(i)))
        return Capital(AS_NOUN_OR_ADJECTIVE);
    if (next.isEmpty() && !title.QuoteAfter(i))
        return Capital("is used as an adverb or noun");
    return Lower("is probably used as a preposition");
}

Decision DecideNearOrMid(const ParsedTitle& /*title*/, int /*i*/, const QString& prev, const QString& next) {
    if (Determiners().contains(prev))
        return Capital(AS_NOUN_OR_ADJECTIVE);
    if (Determiners().contains(next) || ObjectPronouns().contains(next))
        return Lower("is used as a preposition");
    return Capital("is used as a noun, adjective or adverb");
}

Decision DecideTillOrThru(const ParsedTitle& /*title*/, int /*i*/, const QString& /*prev*/, const QString& next) {
    if (ClauseStarters().contains(next))
        return Capital("is used as a conjunction");
    return Lower("is used as a preposition");
}

Decision DecideRule(const QString& key, const ParsedTitle& title, int i) {
    if (Particles().contains(key))
        return DecideParticle(title, i);

    if (key == "a") {
        if (title.JointBefore(i).contains('&') || title.JointBefore(i + 1).contains('&'))
            return Capital(AS_LETTER);
        const bool hyphenBefore = title.HyphenBefore(i);
        const bool hyphenAfter = title.HyphenAfter(i);
        if (hyphenBefore && hyphenAfter)
            return Lower("is an article inside a hyphenated compound");
        if (hyphenBefore || hyphenAfter || !title.PlainAfter(i) || title.Word(i).endsWith('.'))
            return Capital(AS_LETTER);
        return Lower("is an article (if it is meant as the letter A, please reword the title to avoid a capital A)");
    }

    const QString prev = title.PrevKey(i);
    const QString next = title.NextKey(i);

    if (key == "than") {
        if (ClauseStarters().contains(next) || (prev == "said" && next == "done"))
            return Capital("is used as a conjunction");
        return Lower("is used as a preposition");
    }

    if (key == "like") {
        const QString before = title.AdjacentPrev(i);
        if (LikePrepositionAfter().contains(before))
            return Lower(AS_PREPOSITION);
        if (ClauseStarters().contains(before))
            return Capital("is used as a verb");
        if (Articles().contains(title.AdjacentNext(i)))
            return Lower(AS_PREPOSITION);
        return Capital("is probably used as a verb, an adjective or a conjunction");
    }

    if (key == "past" || key == "plus")
        return DecidePastOrPlus(title, i, prev, next);
    if (key == "near" || key == "mid")
        return DecideNearOrMid(title, i, prev, next);
    if (key == "thru" || key == "til" || key == "'til" || key == "'till" || key == "till")
        return DecideTillOrThru(title, i, prev, next);

    return Keep("");
}

bool HasContextRule(const QString& key) {
    if (Particles().contains(key))
        return true;
    static const QSet<QString> extras = {"a", "than", "like", "past", "plus", "near", "mid", "thru", "til", "'til", "'till", "till"};
    return extras.contains(key);
}

Decision Decide(const ParsedTitle& title, int i) {
    const QString& word = title.Word(i);
    const QString& key = title.Keys[i];
    const bool hyphenBefore = title.HyphenBefore(i);

    if (!LetterRe().match(word).hasMatch())
        return Keep("contains no letters");
    if (NonLatinRe().match(word).hasMatch())
        return Keep("is not a Latin-script word");
    if (IsAllCaps(word))
        return Keep("is written in all caps, assumed intentional");
    if (HasInnerCapital(word))
        return Keep("has internal capitals (brand or name), left unchanged");
    if (DigitRe().match(word).hasMatch())
        return Keep("contains digits, left unchanged");
    if (ClippedOfRe().match(word).hasMatch())
        return Lower("is a diminished form of \"of\"");
    if (key == "w" && i + 1 < title.Length() && title.JointBefore(i + 1).startsWith('/'))
        return Lower("is an abbreviation of \"with\"");
    if (ClippedAndRe().match(word).hasMatch() || ClippedAboutRe().match(word).hasMatch())
        return Lower("is a clipped form of \"and\" / \"about\"");
    if (word == "V")
        return Keep("is a single capital V (roman numeral or initial), left unchanged");
    if (title.StartsClause(i) && !hyphenBefore)
        return Capital(i == 0 ? "is the first word of the title" : "is the first word of a subtitle or sentence");
    if (title.EndsClause(i) && !title.HyphenAfter(i))
        return Capital("is the last word of the title or subtitle");
    if (HasContextRule(key))
        return DecideRule(key, title, i);

    auto it = LowercaseReasons().constFind(key);
    if (it != LowercaseReasons().constEnd())
        return Lower(it.value());
    return Capital(DEFAULT_REASON);
}

} // namespace

TitleCaseAnalyzer::Result TitleCaseAnalyzer::Analyze(const QString& phrase) {
    const ParsedTitle title(phrase);
    Result result;

    QString suggestion;
    for (int i = 0; i < title.Length(); ++i) {
        const QString& word = title.Word(i);
        const Decision decision = Decide(title, i);
        QString cased = word;
        if (decision.Cap.has_value()) {
            cased = decision.Cap.value() ? CapitalizeWord(word) : word.toLower();
        }
        suggestion += title.JointBefore(i) + cased;
        if (cased != word) {
            result.Changes.append({word, cased, decision.Reason});
        }
    }
    suggestion += title.LastJoint;
    result.Suggestion = suggestion;
    return result;
}

} // namespace RALE
