import http from 'k6/http';
import { check, sleep } from 'k6';

// Configuration for the load test
export const options = {
    stages: [
        { duration: '10s', target: 1 }, // Ramp up to 10 users over 1 minute
        { duration: '20s', target: 1 }, // Stay at 10 users for 2 minutes
        { duration: '10s', target: 0 },  // Ramp down to 0 users over 1 minute
    ],
};

// Function to generate a unique ID
function generateUniqueId() {
    return `${Date.now()}-${Math.floor(Math.random() * 1000)}`;
}


//k6 run load_test.js

export default function () {
    // Define the URL for POST and GET requests
    const postUrl = 'http://localhost:5000/Products';
    const baseGetUrl = 'http://localhost:5000/Products/';

    // Generate a unique ID for POST request
    const uniqueId = generateUniqueId();

    // Payload for POST request
    const payload = JSON.stringify({
        id: uniqueId,
        name: `string-${uniqueId}`, // Optional: include the unique ID in the name
        price: Math.floor(Math.random() * 100), // Random price between 0 and 99
        userId: 'test',
    });

    const postParams = {
        headers: {
            'Content-Type': 'application/json',
            'accept': 'text/plain',
        },
    };

    // POST request
    const postResponse = http.post(postUrl, payload, postParams);

    // Optional: Add checks to ensure the POST request was successful
    check(postResponse, {
        'POST is status 200': (r) => r.status === 200,
        'POST is content type text/plain': (r) => r.headers['Content-Type'] === 'text/plain',
    });

    // Optional delay before making GET requests
    sleep(1);

    // GET request URL
    const getUrl = `${baseGetUrl}${uniqueId}`;

    const getParams = {
        headers: {
            'accept': 'text/plain',
        },
    };

    // GET request
    const getResponse = http.get(getUrl, getParams);

    // Optional: Add checks to ensure the GET request was successful
    check(getResponse, {
        'GET is status 200': (r) => r.status === 200,
        'GET is content type text/plain': (r) => r.headers['Content-Type'] === 'text/plain',
    });

    // Optional delay to simulate user think time
    sleep(1);
}
