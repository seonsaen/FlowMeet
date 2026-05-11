import { Outlet, useNavigate } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import { MobileNav } from './MobileNav';

export const MainLayout = () => {
    const navigate = useNavigate();

    const openCreateEvent = () => {
        navigate('/schedule?create=1');
    };

    return (
        <div className="h-screen w-screen bg-gray-50 font-sans flex flex-col md:flex-row overflow-hidden">
            <Sidebar />
            
            <div className="flex-1 flex flex-col h-full bg-gray-50 relative overflow-hidden">
                <div className="flex-1 overflow-y-auto scrollbar-hide pb-20 md:pb-6 relative">
                    {/* Outlet will render current page */}
                    <Outlet />
                </div>
                
                <MobileNav onOpenCreateModal={openCreateEvent} />
            </div>
        </div>
    );
};
