import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://localhost:5005';

export const options = {
  scenarios: {
    steady_load: {
      executor: 'constant-vus',
      vus: Number(__ENV.VUS || 20),
      duration: __ENV.DURATION || '1m',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
  },
};

export default function () {
  const requests = [
    ['GET', `${baseUrl}/api/v1/stories/10`],
    ['GET', `${baseUrl}/api/v1/stories/beststories?page=1&pageSize=20`],
    ['GET', `${baseUrl}/api/v1/stories/topstories?page=1&pageSize=20`],
    ['GET', `${baseUrl}/api/v1/stories/newstories?page=1&pageSize=20`],
  ];

  for (const [, url] of requests) {
    const response = http.get(url);
    check(response, {
      'status is 200': (r) => r.status === 200,
      'response is json': (r) => (r.headers['Content-Type'] || '').includes('application/json'),
    });
  }

  sleep(1);
}
