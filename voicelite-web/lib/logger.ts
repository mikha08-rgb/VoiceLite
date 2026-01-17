/**
 * Server-side logger utility for VoiceLite web backend
 * Environment-aware, with automatic sensitive data redaction
 */

type LogContext = Record<string, unknown>;

enum LogLevel {
  Debug = 0,
  Info = 1,
  Warning = 2,
  Error = 3,
}

// Production: warn/error only, Development: all levels
const MIN_LOG_LEVEL = process.env.NODE_ENV === 'production'
  ? LogLevel.Warning
  : LogLevel.Debug;

const SENSITIVE_KEYS = ['licenseKey', 'password', 'token', 'secret', 'apiKey', 'authorization', 'webhookSecret'];

function redactSensitive(obj: LogContext): LogContext {
  const result: LogContext = {};
  for (const [key, value] of Object.entries(obj)) {
    const lowerKey = key.toLowerCase();
    if (SENSITIVE_KEYS.some(sk => lowerKey.includes(sk.toLowerCase()))) {
      result[key] = '<redacted>';
    } else if (Array.isArray(value)) {
      result[key] = value.map(item =>
        typeof item === 'object' && item !== null
          ? redactSensitive(item as LogContext)
          : item
      );
    } else if (typeof value === 'object' && value !== null) {
      result[key] = redactSensitive(value as LogContext);
    } else {
      result[key] = value;
    }
  }
  return result;
}

function formatContext(context?: LogContext): string {
  if (!context || Object.keys(context).length === 0) return '';
  return ' ' + JSON.stringify(redactSensitive(context));
}

function log(level: LogLevel, message: string, context?: LogContext) {
  if (level < MIN_LOG_LEVEL) return;

  const timestamp = new Date().toISOString();
  const levelStr = LogLevel[level].toUpperCase().padEnd(5);
  const formatted = `[${timestamp}] [${levelStr}] ${message}${formatContext(context)}`;

  switch (level) {
    case LogLevel.Error:
      console.error(formatted);
      break;
    case LogLevel.Warning:
      console.warn(formatted);
      break;
    default:
      console.log(formatted);
  }
}

export const logger = {
  debug: (message: string, context?: LogContext) => log(LogLevel.Debug, message, context),
  info: (message: string, context?: LogContext) => log(LogLevel.Info, message, context),
  warn: (message: string, context?: LogContext) => log(LogLevel.Warning, message, context),
  error: (message: string, error?: Error | unknown, context?: LogContext) => {
    const errorContext: LogContext = {
      ...context,
      error: error instanceof Error
        ? { message: error.message, stack: error.stack }
        : String(error),
    };
    log(LogLevel.Error, message, errorContext);
  },
};
