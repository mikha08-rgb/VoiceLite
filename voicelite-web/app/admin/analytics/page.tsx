'use client';

/**
 * Analytics Dashboard Page
 *
 * Visualizes usage analytics with interactive charts
 * Data fetched from /api/admin/analytics
 */

import { useEffect, useState } from 'react';
import { LineChart, Line, BarChart, Bar, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { Activity, Users, TrendingUp, AlertCircle, ArrowLeft } from 'lucide-react';
import Link from 'next/link';

interface AnalyticsData {
  overview: {
    totalEvents: number;
    dailyActiveUsers: number;
    monthlyActiveUsers: number;
    dau_mau_ratio: string;
  };
  events: {
    byType: Record<string, number>;
  };
  users: {
    tierDistribution: Record<string, number>;
  };
  versions: {
    distribution: Array<{ version: string | null; count: number }>;
  };
  models: {
    distribution: Array<{ model: string | null; count: number }>;
  };
  os: {
    distribution: Array<{ os: string | null; count: number }>;
  };
  timeSeries: {
    daily: Array<{ date: string; count: number }>;
  };
  generatedAt: string;
  dateRange: {
    start: string;
    end: string;
    days: number;
  };
}

const COLORS = ['#8b5cf6', '#06b6d4', '#10b981', '#f59e0b', '#ef4444', '#ec4899'];

export default function AnalyticsPage() {
  const [data, setData] = useState<AnalyticsData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [days, setDays] = useState(30);

  useEffect(() => {
    fetchAnalytics();
  }, [days]);

  const fetchAnalytics = async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch(`/api/admin/analytics?days=${days}`, {
        cache: 'no-store',
      });

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Unauthorized. Admin access required.');
        }
        throw new Error('Failed to fetch analytics');
      }

      const analyticsData = await response.json();
      setData(analyticsData);
    } catch (err) {
      console.error('Failed to fetch analytics:', err);
      setError(err instanceof Error ? err.message : 'Failed to load analytics');
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <main className="min-h-screen bg-stone-50 px-6 py-20 dark:bg-[#0f0f12]">
        <div className="mx-auto max-w-7xl">
          <div className="animate-pulse space-y-8">
            <div className="h-12 w-64 rounded-lg bg-stone-200 dark:bg-stone-800"></div>
            <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
              {[1, 2, 3, 4].map((i) => (
                <div key={i} className="h-32 rounded-2xl bg-stone-200 dark:bg-stone-800"></div>
              ))}
            </div>
            <div className="h-96 rounded-2xl bg-stone-200 dark:bg-stone-800"></div>
          </div>
        </div>
      </main>
    );
  }

  if (error) {
    return (
      <main className="min-h-screen bg-stone-50 px-6 py-20 dark:bg-[#0f0f12]">
        <div className="mx-auto max-w-2xl">
          <div className="rounded-2xl border border-red-200 bg-red-50 p-8 text-center dark:border-red-800 dark:bg-red-950/50">
            <AlertCircle className="mx-auto mb-4 h-12 w-12 text-red-600 dark:text-red-400" />
            <h2 className="mb-2 text-2xl font-bold text-stone-900 dark:text-stone-50">
              Access Denied
            </h2>
            <p className="text-stone-700 dark:text-stone-300">{error}</p>
            <Link
              href="/admin"
              className="mt-6 inline-block rounded-full bg-purple-600 px-6 py-2 font-semibold text-white hover:bg-purple-700"
            >
              Back to Admin Dashboard
            </Link>
          </div>
        </div>
      </main>
    );
  }

  if (!data) {
    return null;
  }

  // Prepare chart data
  const eventTypeData = Object.entries(data.events.byType).map(([name, value]) => ({
    name: name.replace(/_/g, ' '),
    value,
  }));

  const tierData = Object.entries(data.users.tierDistribution).map(([name, value]) => ({
    name,
    users: value,
  }));

  const modelData = data.models.distribution
    .filter((m) => m.model)
    .slice(0, 6)
    .map((m) => ({
      name: m.model?.replace('ggml-', '').replace('.bin', '') || 'Unknown',
      count: m.count,
    }));

  const osData = data.os.distribution
    .filter((o) => o.os)
    .slice(0, 6)
    .map((o) => ({
      name: o.os || 'Unknown',
      count: o.count,
    }));

  return (
    <main className="min-h-screen bg-stone-50 px-6 py-12 dark:bg-[#0f0f12]">
      <div className="mx-auto max-w-7xl space-y-8">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <Link
              href="/admin"
              className="mb-4 inline-flex items-center gap-2 text-sm text-stone-600 hover:text-stone-900 dark:text-stone-400 dark:hover:text-stone-50"
            >
              <ArrowLeft className="h-4 w-4" />
              Back to Admin
            </Link>
            <h1 className="text-4xl font-bold text-stone-900 dark:text-stone-50">
              Analytics Dashboard
            </h1>
            <p className="mt-2 text-sm text-stone-600 dark:text-stone-400">
              Last {data.dateRange.days} days · Updated{' '}
              {new Date(data.generatedAt).toLocaleString()}
            </p>
          </div>

          <div className="flex items-center gap-4">
            <select
              value={days}
              onChange={(e) => setDays(Number(e.target.value))}
              className="rounded-lg border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-stone-900 dark:border-stone-700 dark:bg-stone-800 dark:text-stone-50"
            >
              <option value={7}>Last 7 days</option>
              <option value={30}>Last 30 days</option>
              <option value={90}>Last 90 days</option>
              <option value={365}>Last year</option>
            </select>
            <button
              onClick={fetchAnalytics}
              className="rounded-full bg-purple-600 px-6 py-2 font-semibold text-white hover:bg-purple-700"
            >
              Refresh
            </button>
          </div>
        </div>

        {/* Key Metrics */}
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
          <MetricCard
            icon={Activity}
            title="Total Events"
            value={data.overview.totalEvents.toLocaleString()}
            subtitle={`${data.dateRange.days} days`}
            color="purple"
          />
          <MetricCard
            icon={Users}
            title="Daily Active Users"
            value={data.overview.dailyActiveUsers}
            subtitle="Last 7 days"
            color="cyan"
          />
          <MetricCard
            icon={Users}
            title="Monthly Active Users"
            value={data.overview.monthlyActiveUsers}
            subtitle="Last 30 days"
            color="green"
          />
          <MetricCard
            icon={TrendingUp}
            title="DAU/MAU Ratio"
            value={`${(parseFloat(data.overview.dau_mau_ratio) * 100).toFixed(0)}%`}
            subtitle="Engagement score"
            color="orange"
          />
        </div>

        {/* Daily Events Timeline */}
        <ChartCard title="Daily Activity" subtitle="Events over time">
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={data.timeSeries.daily}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-stone-200 dark:stroke-stone-700" />
              <XAxis
                dataKey="date"
                className="text-xs text-stone-600 dark:text-stone-400"
                tickFormatter={(value) => new Date(value).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}
              />
              <YAxis className="text-xs text-stone-600 dark:text-stone-400" />
              <Tooltip
                contentStyle={{
                  backgroundColor: 'rgba(255, 255, 255, 0.95)',
                  border: '1px solid #e5e7eb',
                  borderRadius: '0.5rem',
                }}
                labelFormatter={(value) => new Date(value).toLocaleDateString()}
              />
              <Line type="monotone" dataKey="count" stroke="#8b5cf6" strokeWidth={2} dot={{ r: 3 }} />
            </LineChart>
          </ResponsiveContainer>
        </ChartCard>

        {/* Charts Grid */}
        <div className="grid gap-6 lg:grid-cols-2">
          {/* Event Types Pie Chart */}
          <ChartCard title="Event Types" subtitle="Distribution of event types">
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={eventTypeData}
                  cx="50%"
                  cy="50%"
                  labelLine={false}
                  label={({ name, percent }) => `${name}: ${(percent * 100).toFixed(0)}%`}
                  outerRadius={80}
                  fill="#8884d8"
                  dataKey="value"
                >
                  {eventTypeData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
          </ChartCard>

          {/* Tier Distribution */}
          <ChartCard title="User Tiers" subtitle="Free vs Pro distribution">
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={tierData}>
                <CartesianGrid strokeDasharray="3 3" className="stroke-stone-200 dark:stroke-stone-700" />
                <XAxis dataKey="name" className="text-xs text-stone-600 dark:text-stone-400" />
                <YAxis className="text-xs text-stone-600 dark:text-stone-400" />
                <Tooltip />
                <Bar dataKey="users" fill="#8b5cf6" radius={[8, 8, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>

          {/* Model Usage */}
          <ChartCard title="Model Usage" subtitle="Top models by transcription count">
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={modelData} layout="vertical">
                <CartesianGrid strokeDasharray="3 3" className="stroke-stone-200 dark:stroke-stone-700" />
                <XAxis type="number" className="text-xs text-stone-600 dark:text-stone-400" />
                <YAxis dataKey="name" type="category" className="text-xs text-stone-600 dark:text-stone-400" width={100} />
                <Tooltip />
                <Bar dataKey="count" fill="#06b6d4" radius={[0, 8, 8, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>

          {/* OS Distribution */}
          <ChartCard title="OS Distribution" subtitle="Operating systems">
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={osData}>
                <CartesianGrid strokeDasharray="3 3" className="stroke-stone-200 dark:stroke-stone-700" />
                <XAxis dataKey="name" className="text-xs text-stone-600 dark:text-stone-400" angle={-45} textAnchor="end" height={80} />
                <YAxis className="text-xs text-stone-600 dark:text-stone-400" />
                <Tooltip />
                <Bar dataKey="count" fill="#10b981" radius={[8, 8, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        </div>
      </div>
    </main>
  );
}

function MetricCard({
  icon: Icon,
  title,
  value,
  subtitle,
  color,
}: {
  icon: any;
  title: string;
  value: string | number;
  subtitle: string;
  color: 'purple' | 'cyan' | 'green' | 'orange';
}) {
  const colorClasses = {
    purple: 'bg-purple-100 text-purple-600 dark:bg-purple-950 dark:text-purple-400',
    cyan: 'bg-cyan-100 text-cyan-600 dark:bg-cyan-950 dark:text-cyan-400',
    green: 'bg-green-100 text-green-600 dark:bg-green-950 dark:text-green-400',
    orange: 'bg-orange-100 text-orange-600 dark:bg-orange-950 dark:text-orange-400',
  };

  return (
    <div className="rounded-2xl border border-stone-200 bg-white p-6 dark:border-stone-800 dark:bg-stone-900">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-sm font-medium text-stone-600 dark:text-stone-400">{title}</p>
          <p className="mt-2 text-3xl font-bold text-stone-900 dark:text-stone-50">{value}</p>
          <p className="mt-1 text-xs text-stone-500 dark:text-stone-500">{subtitle}</p>
        </div>
        <div className={`rounded-full p-3 ${colorClasses[color]}`}>
          <Icon className="h-5 w-5" />
        </div>
      </div>
    </div>
  );
}

function ChartCard({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-2xl border border-stone-200 bg-white p-6 dark:border-stone-800 dark:bg-stone-900">
      <div className="mb-4">
        <h3 className="text-lg font-bold text-stone-900 dark:text-stone-50">{title}</h3>
        <p className="text-sm text-stone-600 dark:text-stone-400">{subtitle}</p>
      </div>
      {children}
    </div>
  );
}
