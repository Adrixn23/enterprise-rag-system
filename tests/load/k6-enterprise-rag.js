import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5235';
const TEST_EMAIL = __ENV.TEST_EMAIL || '';
const TEST_PASSWORD = __ENV.TEST_PASSWORD || '';

const listDuration = new Trend('rag_documents_list_ms');
const searchDuration = new Trend('rag_search_ms');
const chatDuration = new Trend('rag_chat_ms');
const errorRate = new Rate('rag_errors');

export const options = {
  stages: [
    { duration: '30s', target: 5 },
    { duration: '1m', target: 15 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.05'],
    rag_documents_list_ms: ['p(95)<500', 'p(99)<1000'],
    rag_search_ms: ['p(95)<2000'],
    rag_chat_ms: ['p(95)<6000'],
    rag_errors: ['rate<0.05'],
  },
};

export function setup() {
  if (!TEST_EMAIL || !TEST_PASSWORD) {
    throw new Error('Define TEST_EMAIL y TEST_PASSWORD como variables de entorno para ejecutar k6.');
  }

  const loginRes = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email: TEST_EMAIL, password: TEST_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  check(loginRes, { 'login status 200': (r) => r.status === 200 });
  const body = loginRes.json();
  return { token: body.token };
}

export default function (data) {
  const headers = {
    Authorization: `Bearer ${data.token}`,
    'Content-Type': 'application/json',
  };

  // 1. Listado paginado de documentos
  const listRes = http.get(`${BASE_URL}/api/v1/documents?page=1&pageSize=20`, { headers });
  listDuration.add(listRes.timings.duration);
  const listOk = check(listRes, { 'documents list 200': (r) => r.status === 200 });
  errorRate.add(!listOk);

  // 2. Retrieval vectorial
  const searchRes = http.post(
    `${BASE_URL}/api/v1/search`,
    JSON.stringify({ query: 'política de seguridad', limit: 5, minScore: 0.4 }),
    { headers }
  );
  searchDuration.add(searchRes.timings.duration);
  const searchOk = check(searchRes, { 'search 200': (r) => r.status === 200 });
  errorRate.add(!searchOk);

  // 3. Chat RAG
  const chatRes = http.post(
    `${BASE_URL}/api/v1/chat`,
    JSON.stringify({ message: 'Resume las políticas principales' }),
    { headers }
  );
  chatDuration.add(chatRes.timings.duration);
  const chatOk = check(chatRes, { 'chat 200': (r) => r.status === 200 });
  errorRate.add(!chatOk);

  // 4. Health check readiness
  const healthRes = http.get(`${BASE_URL}/health/ready`);
  check(healthRes, { 'health ready 200': (r) => r.status === 200 });

  sleep(1);
}
