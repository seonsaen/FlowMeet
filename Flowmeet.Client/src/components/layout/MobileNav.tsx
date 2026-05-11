import { Calendar, Home, Plus, User, Users, type AppIconComponent } from '../icons/hugeIcons';
import { Link, useLocation } from 'react-router-dom';

interface MobileNavProps {
    onOpenCreateModal: () => void;
}

interface TabButtonProps {
    to: string;
    icon: AppIconComponent;
    label: string;
    pathname: string;
}

const TabButton = ({ to, icon: Icon, label, pathname }: TabButtonProps) => {
    const isActive = pathname === to || (to === '/' && pathname === '/dashboard');
    return (
        <Link
            to={to}
            className={`flex flex-col items-center justify-center w-16 h-full transition-colors ${
                isActive ? 'text-indigo-600' : 'text-gray-400 hover:text-gray-600'
            }`}
        >
            <Icon size={20} className="mb-1" strokeWidth={isActive ? 2.5 : 2} />
            <span className="text-[10px] font-medium">{label}</span>
        </Link>
    );
};

export const MobileNav = ({ onOpenCreateModal }: MobileNavProps) => {
    const location = useLocation();

    return (
        <>
            {/* Mobile Floating Action Button (FAB) */}
            <div className="md:hidden fixed bottom-20 right-4 z-20">
                <button 
                    onClick={onOpenCreateModal}
                    className="bg-indigo-600 hover:bg-indigo-700 text-white p-4 rounded-full shadow-lg shadow-indigo-300 transition-transform active:scale-95 flex items-center justify-center"
                >
                    <Plus size={24} strokeWidth={3} />
                </button>
            </div>
            
            {/* Mobile Bottom Navigation */}
            <div className="md:hidden h-16 bg-white border-t border-gray-100 flex justify-around items-center px-2 fixed bottom-0 w-full z-30 shadow-[0_-10px_40px_rgba(0,0,0,0.05)]">
                <TabButton to="/dashboard" icon={Home} label="Главная" pathname={location.pathname} />
                <TabButton to="/schedule" icon={Calendar} label="План" pathname={location.pathname} />
                <TabButton to="/network" icon={Users} label="Люди" pathname={location.pathname} />
                <TabButton to="/profile" icon={User} label="Профиль" pathname={location.pathname} />
            </div>
        </>
    );
};
