'use client';

import { useEffect, useState } from 'react';
import { Users, Key, MessageSquare, Activity, TrendingUp, AlertCircle } from 'lucide-react';
import Link from 'next/link';

interface Stats {
  users: {
    total: number;
    new7d: number;
    new30d: number;
    active30d: number;
    growth: Array<{ date: string; count: number }>;
  };
  licenses: {
    total: number;
    active: number;
    byType: Record<string, number>;
    activations: {
      total: number;
      active: number;
    };
  };
  purchases: {
    total: number;
  };
  feedback: {
    byStatus: Record<string, number>;
    total: number;
  };
  activity: {
    breakdown: Record<string, number>;
  };
  generatedAt: string;
}

export default function AdminDashboard() {
  const [stats, setStats] = useState<Stats | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchStats();
  }, []);

  const fetchStats = async () => {
    try {
      const response = await fetch('/api/admin/stats', {
        cache: 'no-store',
      });

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Unauthorized. Admin access required.');
        }
        throw new Error('Failed to fetch stats');
      }

      const data = await response.json();
      setStats(data);
    } catch (err) {
      console.error('Failed to fetch stats:', err);
      setError(err instanceof Error ? err.message : 'Failed to load dashboard');
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
              href="/"
              className="mt-6 inline-block rounded-full bg-purple-600 px-6 py-2 font-semibold text-white hover:bg-purple-700"
            >
              Go Home
            </Link>
          </div>
        </div>
      </main>
    );
  }

  if (!stats) {
    return null;
  }

  return (
    <main className="min-h-screen bg-stone-50 px-6 py-12 dark:bg-[#0f0f12]">
      <div className="mx-auto max-w-7xl space-y-8">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-4xl font-bold text-stone-900 dark:text-stone-50">
              Admin Dashboard
            </h1>
            <p className="mt-2 text-sm text-stone-600 dark:text-stone-400">
              Last updated: {new Date(stats.generatedAt).toLocaleString()}
            </p>
          </div>
          <button
            onClick={fetchStats}
            className="rounded-full bg-purple-600 px-6 py-2 font-semibold text-white hover:bg-purple-700"
          >
            Refresh
          </button>
        </div>

        {/* Key Metrics */}
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
          <StatCard
            icon={Users}
            title="Total Users"
            value={stats.users.total}
            subtitle={`+${stats.users.new30d} this month`}
            trend={stats.users.new30d > 0 ? 'up' : 'neutral'}
          />
          <StatCard
            icon={Key}
            title="Active Licenses"
            value={stats.licenses.active}
            subtitle={`${stats.licenses.total} total`}
            trend="neutral"
          />
          <StatCard
            icon={TrendingUp}
            title="Active Users (30d)"
            value={stats.users.active30d}
            subtitle={`${((stats.users.active30d / stats.users.total) * 100).toFixed(1)}% of total`}
            trend={stats.users.active30d > stats.users.total / 2 ? 'up' : 'neutral'}
          />
          <StatCard
            icon={MessageSquare}
            title="Feedback"
            value={stats.feedback.total}
            subtitle={`${stats.feedback.byStatus.OPEN || 0} open`}
            trend={stats.feedback.byStatus.OPEN > 0 ? 'up' : 'neutral'}
          />
        </div>

        {/* Detailed Stats */}
        <div className="grid gap-6 lg:grid-cols-2">
          {/* License Breakdown */}
          <div className="rounded-2xl border border-stone-200 bg-white p-6 dark:border-stone-800 dark:bg-stone-900">
            <h3 className="mb-4 text-lg font-bold text-stone-900 dark:text-stone-50">
              License Distribution
            </h3>
            <div className="space-y-3">
              {Object.entries(stats.licenses.byType).map(([type, count]) => (
                <div key={type} className="flex items-center justify-between">
                  <span className="text-stone-700 dark:text-stone-300">{type}</span>
                  <span className="font-semibold text-stone-900 dark:text-stone-50">{count}</span>
                </div>
              ))}
              <div className="mt-4 border-t border-stone-200 pt-3 dark:border-stone-700">
                <div className="flex items-center justify-between">
                  <span className="text-stone-700 dark:text-stone-300">Device Activations</span>
                  <span className="font-semibold text-stone-900 dark:text-stone-50">
                    {stats.licenses.activations.active}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Feedback Breakdown */}
          <div className="rounded-2xl border border-stone-200 bg-white p-6 dark:border-stone-800 dark:bg-stone-900">
            <h3 className="mb-4 text-lg font-bold text-stone-900 dark:text-stone-50">
              Feedback Status
            </h3>
            <div className="space-y-3">
              {Object.entries(stats.feedback.byStatus).map(([status, count]) => (
                <div key={status} className="flex items-center justify-between">
                  <span className="text-stone-700 dark:text-stone-300">{status}</span>
                  <span className="font-semibold text-stone-900 dark:text-stone-50">{count}</span>
                </div>
              ))}
            </div>
            <Link
              href="/admin/feedback"
              className="mt-4 inline-block rounded-full bg-purple-600 px-4 py-2 text-sm font-semibold text-white hover:bg-purple-700"
            >
              View All Feedback
            </Link>
          </div>
        </div>

        {/* Recent Activity */}
        <div className="rounded-2xl border border-stone-200 bg-white p-6 dark:border-stone-800 dark:bg-stone-900">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-lg font-bold text-stone-900 dark:text-stone-50">
              Activity Breakdown (Last 30 Days)
            </h3>
            <Link
              href="/admin/analytics"
              className="rounded-full bg-purple-600 px-4 py-2 text-sm font-semibold text-white hover:bg-purple-700"
            >
              View Analytics Dashboard
            </Link>
          </div>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {Object.entries(stats.activity.breakdown).map(([activityType, count]) => (
              <div
                key={activityType}
                className="rounded-xl border border-stone-200 bg-stone-50 px-4 py-3 dark:border-stone-700 dark:bg-stone-800"
              >
                <div className="text-xs font-medium text-stone-600 dark:text-stone-400">
                  {activityType.replace(/_/g, ' ')}
                </div>
                <div className="text-2xl font-bold text-stone-900 dark:text-stone-50">{count}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </main>
  );
}

function StatCard({
  icon: Icon,
  title,
  value,
  subtitle,
  trend,
}: {
  icon: any;
  title: string;
  value: number;
  subtitle: string;
  trend: 'up' | 'down' | 'neutral';
}) {
  return (
    <div className="rounded-2xl border border-stone-200 bg-white p-6 shadow-sm dark:border-stone-800 dark:bg-stone-900">
      <div className="mb-4 flex items-center justify-between">
        <Icon className="h-8 w-8 text-purple-600 dark:text-purple-400" />
        {trend === 'up' && (
          <TrendingUp className="h-5 w-5 text-green-600 dark:text-green-400" />
        )}
      </div>
      <div className="mb-1 text-3xl font-bold text-stone-900 dark:text-stone-50">
        {value.toLocaleString()}
      </div>
      <div className="text-sm text-stone-600 dark:text-stone-400">{title}</div>
      <div className="mt-2 text-xs text-stone-500 dark:text-stone-500">{subtitle}</div>
    </div>
  );
}
