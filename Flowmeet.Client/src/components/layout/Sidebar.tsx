import { Calendar, Home, User, Users, type AppIconComponent } from '../icons/hugeIcons';
import { Link, useLocation } from 'react-router-dom';

interface NavigationItemProps {
    to: string;
    icon: AppIconComponent;
    label: string;
    pathname: string;
}

const NavigationItem = ({ to, icon: Icon, label, pathname }: NavigationItemProps) => {
    const isActive = pathname === to || (to === '/' && pathname === '/dashboard');
    return (
        <Link
            to={to}
            className={`flex items-center gap-3 w-full px-4 py-3 rounded-xl transition-colors ${
                isActive ? 'bg-indigo-600 text-white shadow-md' : 'text-gray-500 hover:bg-gray-100'
            }`}
        >
            <Icon size={20} />
            <span className="font-medium text-sm">{label}</span>
        </Link>
    );
};

export const Sidebar = () => {
    const location = useLocation();

    return (
        <div className="hidden md:flex flex-col w-64 shrink-0 border-r border-gray-200 bg-white p-6">
            <div className="flex items-center gap-2 mb-10 text-indigo-600">
                <div className="w-8 h-8 bg-indigo-100 rounded-xl flex items-center justify-center font-black">F</div>
                <span className="font-bold text-xl text-gray-800 tracking-tight">FlowMeet</span>
            </div>
            
            <nav className="flex-1 space-y-2">
                <NavigationItem to="/dashboard" icon={Home} label="Главная" pathname={location.pathname} />
                <NavigationItem to="/schedule" icon={Calendar} label="План" pathname={location.pathname} />
                <NavigationItem to="/network" icon={Users} label="Люди" pathname={location.pathname} />
                <NavigationItem to="/profile" icon={User} label="Профиль" pathname={location.pathname} />
            </nav>
        </div>
    );
};
