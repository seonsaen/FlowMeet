const BASE_URL = '/api';
const getHeaders = (includeJsonContentType = true) => {
    const headers: Record<string, string> = {};

    if (includeJsonContentType) {
        headers['Content-Type'] = 'application/json';
    }
    
    const userStr = localStorage.getItem('user');
    if (userStr) {
        try {
            const user = JSON.parse(userStr);
            if (user.token) {
                headers['Authorization'] = `Bearer ${user.token}`;
            }
        } catch {
            localStorage.removeItem('user');
        }
    }
    
    return headers;
};

const handleResponse = async (response: Response) => {
    const data = await response.json().catch(() => null);
    
    if (!response.ok) {
        let errorMsg = data?.error || data?.message;
        if (!errorMsg && data?.errors) {
           errorMsg = Object.values(data.errors).flat().join(', ');
        }
        if (!errorMsg && data?.title) {
           errorMsg = data.title;
        }
        throw new Error(errorMsg || `Ошибка HTTP: ${response.status}`);
    }
    
    return data;
};

export const api = {
    async get(url: string) {
        const response = await fetch(`${BASE_URL}${url}`, {
            method: 'GET',
            headers: getHeaders()
        });
        return handleResponse(response);
    },
    
    async post(url: string, body: unknown) {
        const response = await fetch(`${BASE_URL}${url}`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(body)
        });
        return handleResponse(response);
    },

    async put(url: string, body: unknown) {
        const response = await fetch(`${BASE_URL}${url}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(body)
        });
        return handleResponse(response);
    },

    async patch(url: string, body: unknown) {
        const response = await fetch(`${BASE_URL}${url}`, {
            method: 'PATCH',
            headers: getHeaders(),
            body: JSON.stringify(body)
        });
        return handleResponse(response);
    },
    
    async delete(url: string) {
        const response = await fetch(`${BASE_URL}${url}`, {
            method: 'DELETE',
            headers: getHeaders()
        });
        return handleResponse(response);
    }
};
