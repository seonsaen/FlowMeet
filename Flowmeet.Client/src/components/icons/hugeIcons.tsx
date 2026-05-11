import type { ComponentProps, ReactElement } from 'react';
import { HugeiconsIcon } from '@hugeicons/react';
import {
    Appointment02Icon,
    ArrowLeft02Icon,
    ArrowRight02Icon,
    BatteryMedium02Icon,
    BriefcaseIcon,
    BubbleChatAddIcon,
    Calendar03Icon,
    CalendarAdd02Icon,
    Cancel01Icon,
    ChartUpIcon,
    Clock03Icon,
    Coffee02Icon,
    DateTimeIcon,
    Delete02Icon,
    Home03Icon,
    Key01Icon,
    Logout03Icon,
    MailAdd01Icon,
    MailSend01Icon,
    Moon02Icon,
    MoreHorizontalIcon,
    Notification01Icon,
    Notification03Icon,
    PencilEdit02Icon,
    PlusSignIcon,
    RepeatIcon,
    SaveIcon,
    Search01Icon,
    Shield01Icon,
    SparklesIcon,
    Tick02Icon,
    User03Icon,
    UserAdd01Icon,
    UserGroup03Icon,
    WaveIcon,
    ZapIcon
} from '@hugeicons/core-free-icons';

type HugeiconsProps = ComponentProps<typeof HugeiconsIcon>;
type IconGlyph = HugeiconsProps['icon'];

export type AppIconComponent = (props: Omit<HugeiconsProps, 'icon'>) => ReactElement;

const createIcon = (icon: IconGlyph): AppIconComponent => {
    const Icon = ({ strokeWidth = 2, ...props }: Omit<HugeiconsProps, 'icon'>) => (
        <HugeiconsIcon icon={icon} strokeWidth={strokeWidth} {...props} />
    );

    return Icon;
};

export const ArrowRight = createIcon(ArrowRight02Icon);
export const Battery = createIcon(BatteryMedium02Icon);
export const Bell = createIcon(Notification03Icon);
export const BellRing = createIcon(Notification01Icon);
export const Briefcase = createIcon(BriefcaseIcon);
export const Calendar = createIcon(Calendar03Icon);
export const CalendarClock = createIcon(Appointment02Icon);
export const CalendarPlus = createIcon(CalendarAdd02Icon);
export const Check = createIcon(Tick02Icon);
export const ChevronLeft = createIcon(ArrowLeft02Icon);
export const ChevronRight = createIcon(ArrowRight02Icon);
export const Clock = createIcon(Clock03Icon);
export const Clock3 = createIcon(DateTimeIcon);
export const Coffee = createIcon(Coffee02Icon);
export const Home = createIcon(Home03Icon);
export const KeyRound = createIcon(Key01Icon);
export const LogOut = createIcon(Logout03Icon);
export const MailPlus = createIcon(MailAdd01Icon);
export const MessageCircleMore = createIcon(BubbleChatAddIcon);
export const MoonStar = createIcon(Moon02Icon);
export const MoreHorizontal = createIcon(MoreHorizontalIcon);
export const Pencil = createIcon(PencilEdit02Icon);
export const Plus = createIcon(PlusSignIcon);
export const Repeat2 = createIcon(RepeatIcon);
export const Save = createIcon(SaveIcon);
export const Search = createIcon(Search01Icon);
export const Send = createIcon(MailSend01Icon);
export const Shield = createIcon(Shield01Icon);
export const Sparkles = createIcon(SparklesIcon);
export const Trash2 = createIcon(Delete02Icon);
export const TrendingUp = createIcon(ChartUpIcon);
export const User = createIcon(User03Icon);
export const UserPlus = createIcon(UserAdd01Icon);
export const Users = createIcon(UserGroup03Icon);
export const Waves = createIcon(WaveIcon);
export const X = createIcon(Cancel01Icon);
export const Zap = createIcon(ZapIcon);
