import http from 'k6/http';
import { check } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://localhost:5005';
const expectRateLimits = ['1', 'true', 'yes'].includes((__ENV.EXPECT_RATE_LIMITS || '').toLowerCase());
const defaultRate = expectRateLimits ? 120 : 10;

if (expectRateLimits) {
  http.setResponseCallback(http.expectedStatuses({ min: 200, max: 299 }, 429));
}

export const options = {
  scenarios: {
    steady_rate: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.RATE || defaultRate),
      timeUnit: '1m',
      duration: __ENV.DURATION || '1m',
      preAllocatedVUs: Number(__ENV.VUS || 5),
      maxVUs: Number(__ENV.MAX_VUS || 20),
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
      'status is expected': (r) => expectRateLimits ? r.status === 200 || r.status === 429 : r.status === 200,
      'response is json': (r) => (r.headers['Content-Type'] || '').includes('application/json'),
    });
  }
}
